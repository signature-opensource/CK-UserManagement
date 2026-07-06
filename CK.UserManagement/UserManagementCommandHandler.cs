using CK.Core;
using CK.Cris;
using CK.DB.Actor.ActorEMail;
using CK.DB.User.NamedUser;
using CK.DB.Zone;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement;

/// <summary>
/// Handlers for the workspace-user management commands (edit / list). Business logic only (admin
/// authority is expected to be enforced by the command validators), structured monitor logging,
/// defensive try/catch and translatable <see cref="UserMessage"/> answers. Data access goes through
/// <see cref="UserManagementQueries"/>. Invitation and archive/restore handlers live in the
/// <c>CK.UserManagement.UserInvitation</c> and <c>CK.UserManagement.BinnedUser</c> packages.
/// </summary>
public class UserManagementCommandHandler : IScopedAutoService
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
                                                                 ActorEMailTable emailTable,
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

                    // Update the primary e-mail when it changed. AddEMailAsync (avoidAmbiguousEMail) returns
                    // the actor already bound to the address: a different id means it belongs to someone else.
                    if( !string.IsNullOrWhiteSpace( cmd.Email ) )
                    {
                        var currentEmail = await queries.GetPrimaryEmailAsync( ctx, cmd.UserId );
                        if( !string.Equals( currentEmail, cmd.Email, StringComparison.OrdinalIgnoreCase ) )
                        {
                            var boundTo = await emailTable.AddEMailAsync( ctx, actorId, cmd.UserId, cmd.Email, isPrimary: true );
                            if( boundTo != cmd.UserId )
                            {
                                ctx.Monitor.Warn( $"E-mail already used by another user. (Email: {cmd.Email}, BoundTo: {boundTo})" );
                                return _currentCulture.ErrorMessage( "This e-mail address is already used by another user.", "User.EmailAlreadyUsed" );
                            }
                            // Drop the previous primary address so the user keeps a single e-mail.
                            if( !string.IsNullOrWhiteSpace( currentEmail ) )
                            {
                                await emailTable.RemoveEMailAsync( ctx, actorId, cmd.UserId, currentEmail );
                            }
                            ctx.Monitor.Info( $"User's primary e-mail successfully updated. (UserId: {cmd.UserId})" );
                        }
                    }

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
    #endregion
}
