using CK.Core;
using CK.Cris;
using CK.DB.Actor;
using CK.DB.User.NamedUser;
using CK.DB.User.UserPassword;
using CK.DB.Workspace;
using CK.DB.Zone;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement;

/// <summary>
/// Handlers for every command registered by <c>UserManagementTSPackage</c>.
/// Modeled on the ODLM admin/user handlers: business logic only (admin authority is expected to be
/// enforced by the command validators), structured monitor logging, defensive try/catch and
/// translatable <see cref="UserMessage"/> answers. Data access goes through
/// <see cref="UserManagementQueries"/> and <see cref="UserManagementService"/>.
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
    public async Task<List<IPendingInvitation>> GetPlatformPendingInvitationsAsync( ISqlCallContext ctx,
                                                                                    IGetPlatformPendingInvitationsQCommand query,
                                                                                    UserManagementQueries queries )
    {
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetPlatformPendingInvitationsQCommand )} query." ) )
        {
            try
            {
                var invitations = await queries.GetPendingInvitationsAsync( ctx );
                return invitations.ToList();
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return new();
            }
        }
    }

    [CommandHandler]
    public async Task<List<IPendingInvitation>> GetWorkspacePendingInvitationsAsync( ISqlCallContext ctx,
                                                                                     IGetWorkspacePendingInvitationsQCommand query,
                                                                                     UserManagementQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspacePendingInvitationsQCommand )} query. (WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var invitations = await queries.GetPendingInvitationsAsync( ctx, workspaceId );
                return invitations.ToList();
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return new();
            }
        }
    }

    [CommandHandler]
    public async Task<IWorkspaceInvitationData> GetWorkspaceInvitationDataAsync( ISqlCallContext ctx,
                                                                                 IGetWorkspaceInvitationDataQCommand query,
                                                                                 UserManagementQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspaceInvitationDataQCommand )} query. (WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var groups = await queries.GetWorkspaceGroupsAsync( ctx, workspaceId );
                return query.CreateResult( r =>
                {
                    r.Groups.AddRange( groups );
                    // Languages are provided by the Angular side (locales.ts); left empty here.
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
    public async Task<SimpleUserMessage> CreateInvitationAsync( ISqlTransactionCallContext ctx,
                                                                ICreateInvitationCommand cmd,
                                                                UserTable userTable,
                                                                UserManagementService service )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( ICreateInvitationCommand )} command. (ActorId: {actorId}, WorkspaceId: {cmd.CurrentWorkspaceId.GetValueOrDefault()})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    var message = await service.CreateInvitationAsync( ctx, actorId, cmd.CurrentWorkspaceId.GetValueOrDefault(), cmd.Email, cmd.CultureName, cmd.Groups );
                    transaction.Commit();
                    return message;
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
    public async Task<SimpleUserMessage> ResendInvitationsAsync( ISqlTransactionCallContext ctx,
                                                                 IResendInvitationsCommand cmd,
                                                                 UserTable userTable,
                                                                 UserManagementService service )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IResendInvitationsCommand )} command. (ActorId: {actorId}, Count: {cmd.Invitations.Count})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    foreach( var inv in cmd.Invitations )
                    {
                        await service.ResendInvitationAsync( ctx, actorId, inv.Email, inv.CultureName );
                    }
                    transaction.Commit();
                }
                return _currentCulture.InfoMessage( "Invitations were successfully sent.", "CrisSuccess.InvitationsResend" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return _currentCulture.CreateGenericError();
            }
        }
    }

    [CommandHandler]
    public async Task<ICrisBasicCommandResult> ArchiveUsersAsync( ISqlTransactionCallContext ctx,
                                                                  UserMessageCollector collector,
                                                                  IArchiveUsersAdminCommand cmd,
                                                                  UserTable userTable )
    {
        int actorId = cmd.ActorId.GetValueOrDefault();
        int workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IArchiveUsersAdminCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, Count: {cmd.UserIds.Count})" ) )
        {
            var res = cmd.CreateResult();
            if( cmd.UserIds.Count == 0 )
            {
                collector.Error( "No user identifier provided.", "BinnedUser.NoUserId" );
                res.SetUserMessages( collector );
                return res;
            }
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    foreach( var id in cmd.UserIds )
                    {
                        await userTable.ArchiveUserAsync( ctx, actorId, id, workspaceId );
                        ctx.Monitor.Info( $"User successfully archived. (UserId: {id})" );
                    }
                    transaction.Commit();
                }
                collector.Info( $"{cmd.UserIds.Count} user(s) successfully archived.", "BinnedUser.UsersArchived" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                collector.Error( "An error occurred while archiving users.", "BinnedUser.ArchiveFailed" );
            }
            res.SetUserMessages( collector );
            return res;
        }
    }

    [CommandHandler]
    public async Task<ICrisBasicCommandResult> RestoreUsersAsync( ISqlTransactionCallContext ctx,
                                                                  UserMessageCollector collector,
                                                                  IRestoreUsersAdminCommand cmd,
                                                                  UserTable userTable )
    {
        int actorId = cmd.ActorId.GetValueOrDefault();
        int workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IRestoreUsersAdminCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, Count: {cmd.UserIds.Count})" ) )
        {
            var res = cmd.CreateResult();
            if( cmd.UserIds.Count == 0 )
            {
                collector.Error( "No user identifier provided.", "BinnedUser.NoUserId" );
                res.SetUserMessages( collector );
                return res;
            }
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    foreach( var id in cmd.UserIds )
                    {
                        await userTable.RestoreUserAsync( ctx, actorId, id, workspaceId );
                        ctx.Monitor.Info( $"User successfully restored. (UserId: {id})" );
                    }
                    transaction.Commit();
                }
                collector.Info( $"{cmd.UserIds.Count} user(s) successfully restored.", "BinnedUser.UsersRestored" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                collector.Error( "An error occurred while restoring users.", "BinnedUser.RestoreFailed" );
            }
            res.SetUserMessages( collector );
            return res;
        }
    }

    [CommandHandler]
    public async Task<SimpleUserMessage> EditWorkspaceUserAsync( ISqlTransactionCallContext ctx,
                                                                 IEditWorkspaceUserCommand cmd,
                                                                 UserTable userTable,
                                                                 NamedUserTable namedUserTable,
                                                                 UserPasswordTable userPasswordTable,
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

                    if( !string.IsNullOrWhiteSpace( cmd.CultureName ) )
                    {
                        var xlcid = NormalizedCultureInfo.EnsureNormalizedCultureInfo( cmd.CultureName ).Id;
                        await preferredCulturePackage.SetExtendedCultureAsync( ctx, actorId, cmd.UserId, xlcid );
                        await preferredCulturePackage.SetPreferredCultureNameAsync( ctx, actorId, cmd.UserId, cmd.CultureName );
                        ctx.Monitor.Info( $"User's culture successfully set. (CultureName: {cmd.CultureName}, XLCID: {xlcid})" );
                    }

                    if( !string.IsNullOrWhiteSpace( cmd.Password ) )
                    {
                        await userPasswordTable.SetPasswordAsync( ctx, actorId, cmd.UserId, cmd.Password );
                        ctx.Monitor.Info( "User's password successfully set." );
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
    public async Task<IValidateInvitationTokenResult> ValidateInvitationTokenAsync( ISqlCallContext ctx,
                                                                                    IValidateInvitationTokenCommand cmd,
                                                                                    UserManagementService service )
    {
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IValidateInvitationTokenCommand )} command." ) )
        {
            try
            {
                var pendingUser = await service.ValidateInvitationAsync( ctx, cmd.Token );
                return cmd.CreateResult( r =>
                {
                    r.User = pendingUser;
                    r.UserMessage = _currentCulture.InfoMessage( "Your invitation has been validated. Please complete your registration.", "User.InvitationValidated" );
                } );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return cmd.CreateResult( r =>
                {
                    r.UserMessage = _currentCulture.ErrorMessage( "Your invitation is no longer valid. Please contact your administrator.", "User.InvitationError" );
                } );
            }
        }
    }

    [CommandHandler]
    public async Task<SimpleUserMessage> CompleteRegistrationAsync( ISqlTransactionCallContext ctx,
                                                                    ICompleteRegistrationCommand cmd,
                                                                    UserTable userTable,
                                                                    UserManagementService service )
    {
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( ICompleteRegistrationCommand )} command. (Email: {cmd.Email})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    await service.CompleteRegistrationAsync( ctx, cmd.FirstName, cmd.LastName, cmd.Email, cmd.Token, cmd.Password, cmd.CultureName );
                    transaction.Commit();
                }
                return _currentCulture.InfoMessage( "Registration successful. You can now log-in with your credentials.", "User.RegistrationCompleted" );
            }
            catch( ArgumentException e )
            {
                ctx.Monitor.Error( e );
                return _currentCulture.ErrorMessage( "This account cannot be created.", e.Message );
            }
            catch( InvalidOperationException e )
            {
                ctx.Monitor.Error( e );
                return _currentCulture.ErrorMessage( "Your invitation is no longer valid. Please contact your administrator.", e.Message );
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
