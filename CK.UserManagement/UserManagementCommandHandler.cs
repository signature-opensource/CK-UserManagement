using CK.Core;
using CK.Cris;
using CK.DB.Auth;
using CK.DB.User.NamedUser;
using CK.DB.Zone;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement;

/// <summary>
/// Handlers for the workspace-user management commands (edit / list). The core is e-mail-agnostic
/// (UserName only). Business logic only (admin authority is expected to be enforced by the command
/// validators), structured monitor logging, defensive try/catch and translatable
/// <see cref="UserMessage"/> answers. Data access goes through <see cref="UserManagementQueries"/>.
/// E-mail (UserInvitation) and archive/restore (BinnedUser) enrichments live in those packages, which
/// provide their own handlers for the same commands.
/// </summary>
public class UserManagementCommandHandler : IAutoService
{
    readonly CurrentCultureInfo _currentCulture;

    public UserManagementCommandHandler( CurrentCultureInfo currentCulture )
    {
        _currentCulture = currentCulture;
    }

    #region Queries
    [CommandHandler]
    public async Task<IEditWorkspaceUserData> GetWorkspaceUserEditDataAsync( ISqlCallContext ctx,
                                                                             IGetWorkspaceUserEditDataQCommand query,
                                                                             UserManagementQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspaceUserEditDataQCommand )} query. (UserId: {query.UserId}, WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var userGroups = await queries.GetUserWorkspaceGroupsAsync( ctx, workspaceId, query.UserId );
                var workspaceGroups = await queries.GetWorkspaceGroupsAsync( ctx, workspaceId );
                return query.CreateResult( r =>
                {
                    r.UserGroups.AddRange( userGroups );
                    r.WorkspaceGroups.AddRange( workspaceGroups );
                } );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return query.CreateResult();
            }
        }
    }

    [CommandHandler]
    public async Task<List<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx,
                                                                    IGetWorkspaceUsersQCommand query,
                                                                    UserManagementQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspaceUsersQCommand )} query. (WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var users = await queries.GetWorkspaceUsersAsync( ctx, workspaceId );
                return users.ToList();
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return new();
            }
        }
    }
    #endregion

    #region Commands
    [CommandHandler]
    public async Task<SimpleUserMessage> EditWorkspaceUserAsync( ISqlTransactionCallContext ctx,
                                                                 IEditWorkspaceUserCommand cmd,
                                                                 UserTable userTable,
                                                                 NamedUserTable namedUserTable,
                                                                 CK.DB.Zone.GroupTable groupTable,
                                                                 CK.DB.User.PreferredCulture.Package preferredCulturePackage,
                                                                 UserManagementQueries queries )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IEditWorkspaceUserCommand )} command. (ActorId: {actorId}, UserId: {cmd.UserId})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    await userTable.UserNameSetAsync( ctx, actorId, cmd.UserId, cmd.UserName );
                    await namedUserTable.SetNamesAsync( ctx, actorId, cmd.UserId, cmd.FirstName, cmd.LastName );

                    if( cmd.ExtendedCultureId > 0 )
                    {
                        await preferredCulturePackage.SetExtendedCultureAsync( ctx, actorId, cmd.UserId, cmd.ExtendedCultureId );
                        ctx.Monitor.Info( $"User's culture successfully set. (XLCID: {cmd.ExtendedCultureId})" );
                    }

                    var currentGroups = await queries.GetUserWorkspaceGroupIdsAsync( ctx, workspaceId, cmd.UserId );
                    foreach( var g in currentGroups )
                    {
                        if( !cmd.Groups.Contains( g ) )
                        {
                            await groupTable.RemoveUserAsync( ctx, actorId, g, cmd.UserId );
                            ctx.Monitor.Info( $"User removed from group. (UserId: {cmd.UserId}, GroupId: {g})" );
                        }
                    }
                    foreach( var g in cmd.Groups )
                    {
                        await groupTable.AddUserAsync( ctx, actorId, g, cmd.UserId, autoAddUserInZone: true );
                        ctx.Monitor.Info( $"User added to group. (UserId: {cmd.UserId}, GroupId: {g})" );
                    }

                    transaction.Commit();
                    return _currentCulture.InfoMessage( "Workspace user successfully edited.", "CrisSuccess.WorkspaceUserEdited" );
                }
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return _currentCulture.CreateGenericError();
            }
        }
    }

    [CommandHandler]
    public async Task<SimpleUserMessage> CreateWorkspaceUserAsync( ISqlTransactionCallContext ctx,
                                                                   ICreateWorkspaceUserCommand cmd,
                                                                   UserTable userTable,
                                                                   NamedUserTable namedUserTable,
                                                                   CK.DB.Zone.GroupTable groupTable,
                                                                   CK.DB.User.PreferredCulture.Package preferredCulturePackage,
                                                                   CK.DB.User.UserPassword.UserPasswordTable passwordTable,
                                                                   CK.DB.Workspace.Package workspacePackage )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( ICreateWorkspaceUserCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, UserName: {cmd.UserName})" ) )
        {
            if( string.IsNullOrWhiteSpace( cmd.UserName ) )
            {
                ctx.Monitor.Warn( "No user name provided." );
                return _currentCulture.ErrorMessage( "A user name is required.", "User.UserNameRequired" );
            }
            if( string.IsNullOrWhiteSpace( cmd.Password ) )
            {
                ctx.Monitor.Warn( "No password provided." );
                return _currentCulture.ErrorMessage( "A password is required.", "User.PasswordRequired" );
            }
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    // Create the user directly (UserName + culture), then provision a basic-authentication
                    // password so the user can sign in right away. The core stays e-mail-agnostic.
                    var userId = await preferredCulturePackage.CreateUserAsync( ctx, actorId, cmd.UserName.Trim(), cmd.ExtendedCultureId );
                    if( userId <= 0 )
                    {
                        ctx.Monitor.Warn( $"User name already taken. (UserName: {cmd.UserName})" );
                        return _currentCulture.ErrorMessage( "This user name is already taken. Please choose another one.", "User.UserNameAlreadyTaken" );
                    }

                    await passwordTable.CreateOrUpdatePasswordUserAsync( ctx, actorId, userId, cmd.Password, UCLMode.CreateOnly );

                    await namedUserTable.SetNamesAsync( ctx, actorId, userId, cmd.FirstName, cmd.LastName );

                    foreach( var g in cmd.Groups )
                    {
                        await groupTable.AddUserAsync( ctx, actorId, g, userId, autoAddUserInZone: true );
                        ctx.Monitor.Info( $"User added to group. (UserId: {userId}, GroupId: {g})" );
                    }

                    // Land the new user in the workspace it was created for.
                    await workspacePackage.SetUserPreferredWorkspaceAsync( ctx, actorId, userId, workspaceId );

                    transaction.Commit();
                    ctx.Monitor.Info( $"Workspace user created. (UserId: {userId})" );
                    return _currentCulture.InfoMessage( "User successfully created.", "CrisSuccess.WorkspaceUserCreated" );
                }
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return _currentCulture.CreateGenericError();
            }
        }
    }
    #endregion
}
