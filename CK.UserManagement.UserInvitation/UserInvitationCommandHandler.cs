using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// Handlers for the invitation and (anonymous) registration commands. Business logic only (admin
/// authority is enforced by the command validators), structured monitor logging, defensive try/catch
/// and translatable <see cref="UserMessage"/> answers. Data access goes through
/// <see cref="UserInvitationQueries"/> and <see cref="UserManagementService"/>; the shared workspace
/// groups query is served by the core <see cref="UserManagementQueries"/>.
/// </summary>
public class UserInvitationCommandHandler : IScopedAutoService
{
    readonly CurrentCultureInfo _currentCulture;

    public UserInvitationCommandHandler( CurrentCultureInfo currentCulture )
    {
        _currentCulture = currentCulture;
    }

    #region Queries
    [CommandHandler]
    public async Task<List<IPendingInvitation>> GetPlatformPendingInvitationsAsync( ISqlCallContext ctx,
                                                                                    IGetPlatformPendingInvitationsQCommand query,
                                                                                    UserInvitationQueries queries )
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
                                                                                     UserInvitationQueries queries )
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
                    var message = await service.CreateInvitationAsync( ctx, actorId, cmd.CurrentWorkspaceId.GetValueOrDefault(), cmd.Email, cmd.ExtendedCultureId, cmd.Groups );
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
                        await service.ResendInvitationAsync( ctx, actorId, inv.Email, inv.ExtendedCultureId );
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
    public async Task<SimpleUserMessage> DeactivateInvitationsAsync( ISqlTransactionCallContext ctx,
                                                                     IDeactivateInvitationsCommand cmd,
                                                                     UserTable userTable,
                                                                     UserManagementService service )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IDeactivateInvitationsCommand )} command. (ActorId: {actorId}, Count: {cmd.Invitations.Count})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    foreach( var inv in cmd.Invitations )
                    {
                        await service.DeactivateInvitationAsync( ctx, actorId, inv.Email );
                    }
                    transaction.Commit();
                }
                return _currentCulture.InfoMessage( "Invitations were successfully deactivated.", "CrisSuccess.InvitationsDeactivated" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return _currentCulture.CreateGenericError();
            }
        }
    }

    [CommandHandler]
    public async Task<SimpleUserMessage> DestroyInvitationsAsync( ISqlTransactionCallContext ctx,
                                                                  IDestroyInvitationsCommand cmd,
                                                                  UserTable userTable,
                                                                  UserManagementService service )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IDestroyInvitationsCommand )} command. (ActorId: {actorId}, Count: {cmd.Invitations.Count})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    foreach( var inv in cmd.Invitations )
                    {
                        await service.DestroyInvitationByEmailAsync( ctx, inv.Email );
                    }
                    transaction.Commit();
                }
                return _currentCulture.InfoMessage( "Invitations were successfully deleted.", "CrisSuccess.InvitationsDeleted" );
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
                    await service.CompleteRegistrationAsync( ctx, cmd.FirstName, cmd.LastName, cmd.Email, cmd.Token, cmd.Password, cmd.ExtendedCultureId, cmd.UserName );
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
