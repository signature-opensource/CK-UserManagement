using CK.Core;
using CK.Cris;
using CK.DB.Actor.ActorEMail;
using CK.DB.User.NamedUser;
using CK.DB.Zone;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// Command handler for the UserInvitation package. Handles the invitation / (anonymous) registration
/// commands, and provides the e-mail-aware version of the workspace-user edit command (superseding the
/// core UserName-only handler). Business logic only (admin authority is enforced by the command
/// validators), structured monitor logging, defensive try/catch and translatable
/// <see cref="UserMessage"/> answers. Data access goes through <see cref="UserInvitationQueries"/> and
/// <see cref="UserManagementService"/>; shared workspace-group reads use the core
/// <see cref="UserManagementQueries"/>. The e-mail-aware workspace-user listing lives in its own
/// <see cref="UserInvitationWorkspaceUsersHandler"/> so it can be superseded independently.
/// </summary>
public class UserInvitationCommandHandler : IAutoService,
                                            ICommandHandler<IEditWorkspaceUserCommand>
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

    #region Workspace-user edit (e-mail-aware, supersedes the core handler)
    /// <summary>
    /// E-mail-aware workspace-user edit: edits UserName/names/culture/groups (like the core handler)
    /// and, in addition, updates the primary e-mail. Supersedes the core edit handler.
    /// </summary>
    [CommandHandler]
    public async Task<SimpleUserMessage> EditWorkspaceUserAsync( ISqlTransactionCallContext ctx,
                                                                 CK.IO.UserManagement.UserInvitation.IEditWorkspaceUserCommand cmd,
                                                                 UserTable userTable,
                                                                 NamedUserTable namedUserTable,
                                                                 CK.DB.Zone.GroupTable groupTable,
                                                                 CK.DB.User.PreferredCulture.Package preferredCulturePackage,
                                                                 ActorEMailTable emailTable,
                                                                 UserManagementQueries coreQueries,
                                                                 UserInvitationQueries queries )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        // The e-mail lives on the UserInvitation extension of the command (the closing type).
        var email = cmd.Email;
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IEditWorkspaceUserCommand )} command (with e-mail). (ActorId: {actorId}, UserId: {cmd.UserId})" ) )
        {
            try
            {
                using( var transaction = ctx[userTable].BeginTransaction() )
                {
                    await userTable.UserNameSetAsync( ctx, actorId, cmd.UserId, cmd.UserName );
                    await namedUserTable.SetNamesAsync( ctx, actorId, cmd.UserId, cmd.FirstName, cmd.LastName );

                    // Update the primary e-mail when it changed. AddEMailAsync (avoidAmbiguousEMail) returns
                    // the actor already bound to the address: a different id means it belongs to someone else.
                    if( !string.IsNullOrWhiteSpace( email ) )
                    {
                        var currentEmail = await queries.GetPrimaryEmailAsync( ctx, cmd.UserId );
                        if( !string.Equals( currentEmail, email, StringComparison.OrdinalIgnoreCase ) )
                        {
                            var boundTo = await emailTable.AddEMailAsync( ctx, actorId, cmd.UserId, email, isPrimary: true );
                            if( boundTo != cmd.UserId )
                            {
                                ctx.Monitor.Warn( $"E-mail already used by another user. (Email: {email}, BoundTo: {boundTo})" );
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

                    var currentGroups = await coreQueries.GetUserWorkspaceGroupIdsAsync( ctx, workspaceId, cmd.UserId );
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
