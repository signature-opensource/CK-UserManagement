using System.Text;
using CK.Core;
using CK.DB.Actor.ActorEMail;
using CK.DB.Auth;
using CK.DB.User.NamedUser;
using CK.DB.User.UserPassword;
using CK.DB.Zone;
using CK.IO.UserInvitation;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement;

/// <summary>
/// Business logic for workspace invitations and user registration, built on the standard CK.DB
/// packages. Invitations are persisted by <c>CK.DB.UserInvitation</c> (<c>CK.tUserInvitation</c> and
/// its group/provider satellite tables). The invitation is keyed by the invited e-mail
/// (<c>UserTargetAddress</c>, unique platform-wide); the target culture and the groups the user will
/// join are carried by the invitation itself. Each invitation is created on behalf of the
/// administrator who performs the request (its <c>CreatedById</c>), while reads go through
/// <see cref="UserManagementQueries"/> with creator-agnostic queries so any administrator can list
/// or resend any invitation. The (anonymous) registration flow finalizes an invitation by destroying
/// it on behalf of its original creator. The workspace an invitation belongs to is derived from the
/// zone of its groups. Sending the actual e-mail is delegated to <see cref="IUserManagementMailer"/>.
/// </summary>
public class UserManagementService : IAutoService
{
    // The anonymous registration flow has no requesting actor: user provisioning steps that are not
    // tied to a specific administrator are performed on behalf of the system user.
    const int SystemActorId = 1;

    readonly PocoDirectory _pocoDir;
    readonly CurrentCultureInfo _currentCulture;
    readonly CK.DB.UserInvitation.Package _invitationPackage;
    readonly CK.DB.User.PreferredCulture.Package _userPackage;
    readonly ActorEMailTable _emailTable;
    readonly NamedUserTable _namedUserTable;
    readonly UserPasswordTable _passwordTable;
    readonly GroupTable _groupTable;
    readonly UserTable _userTable;
    readonly CK.DB.Workspace.Package _workspacePackage;
    readonly UserManagementQueries _queries;
    readonly IUserManagementMailer _mailer;

    public UserManagementService( PocoDirectory pocoDir,
                                  CurrentCultureInfo currentCulture,
                                  CK.DB.UserInvitation.Package invitationPackage,
                                  CK.DB.User.PreferredCulture.Package userPackage,
                                  ActorEMailTable emailTable,
                                  NamedUserTable namedUserTable,
                                  UserPasswordTable passwordTable,
                                  GroupTable groupTable,
                                  UserTable userTable,
                                  CK.DB.Workspace.Package workspacePackage,
                                  UserManagementQueries queries,
                                  IUserManagementMailer mailer )
    {
        _pocoDir = pocoDir;
        _currentCulture = currentCulture;
        _invitationPackage = invitationPackage;
        _userPackage = userPackage;
        _emailTable = emailTable;
        _namedUserTable = namedUserTable;
        _passwordTable = passwordTable;
        _groupTable = groupTable;
        _userTable = userTable;
        _workspacePackage = workspacePackage;
        _queries = queries;
        _mailer = mailer;
    }

    /// <summary>
    /// Creates the invitation for an e-mail and sends the invitation e-mail, returning the
    /// <see cref="SimpleUserMessage"/> describing the outcome. Because <c>UserTargetAddress</c> is
    /// unique platform-wide, an e-mail can only have a single pending invitation: when one already
    /// exists the invitation is left untouched and an error message is returned (use
    /// <see cref="ResendInvitationAsync"/> to re-activate it instead).
    /// </summary>
    public async Task<SimpleUserMessage> CreateInvitationAsync( ISqlTransactionCallContext ctx, int actorId, int workspaceId, string email, string cultureName, IReadOnlyList<int> groups )
    {
        var existing = await _queries.GetInvitationByEmailAsync( ctx, email );
        if( existing is not null )
        {
            ctx.Monitor.Warn( $"An invitation already exists for this e-mail. (Email: {email}, InvitationId: {existing.InvitationId})" );
            return _currentCulture.ErrorMessage( "An invitation already exists for this e-mail address.", "User.InvitationAlreadyExists" );
        }

        var cultureId = NormalizedCultureInfo.EnsureNormalizedCultureInfo( cultureName ).Id;
        var create = _pocoDir.Create<ICreateUserInvitationCommand>( c =>
        {
            c.ActorId = actorId;
            c.UserTargetAddress = email;
            c.ExpirationDateUtc = DateTime.UtcNow.AddDays( 3 );
            c.IsActive = true;
            c.CultureId = cultureId;
            foreach( var g in groups.Where( g => g > 0 ) ) c.GroupIdentifiers.Add( g );
        } );
        var invitation = await _invitationPackage.CreateUserInvitationAsync( ctx, create );

        ctx.Monitor.Info( $"Invitation created. (Email: {email}, WorkspaceId: {workspaceId}, InvitationId: {invitation.InvitationId})" );
        await SendInvitationMailAsync( ctx, invitation.InvitationId, email, cultureName );

        return _currentCulture.InfoMessage( "Invitation successfully created.", "CrisSuccess.InvitationCreated" );
    }

    /// <summary>
    /// Re-activates a pending invitation (extends its expiration) and resends the e-mail.
    /// </summary>
    public async Task ResendInvitationAsync( ISqlCallContext ctx, int actorId, string email, string cultureName )
    {
        var invitation = await _queries.GetInvitationByEmailAsync( ctx, email );
        if( invitation is null )
        {
            ctx.Monitor.Warn( $"No pending invitation to resend. (Email: {email})" );
            return;
        }

        await _invitationPackage.SetUserInvitationIsActiveAsync( ctx, _pocoDir.Create<ISetUserInvitationIsActiveCommand>( c =>
        {
            c.ActorId = actorId;
            c.InvitationId = invitation.InvitationId;
            c.IsActive = true;
        } ) );
        await _invitationPackage.SetUserInvitationExpirationDateAsync( ctx, _pocoDir.Create<ISetUserInvitationExpirationDateCommand>( c =>
        {
            c.ActorId = actorId;
            c.InvitationId = invitation.InvitationId;
            c.NewExpirationDate = DateTime.UtcNow.AddDays( 3 );
        } ) );

        ctx.Monitor.Info( $"Invitation re-activated. (Email: {email})" );
        await SendInvitationMailAsync( ctx, invitation.InvitationId, email, cultureName );
    }

    /// <summary>
    /// Validates an invitation secret and returns the pending user (e-mail + default culture).
    /// </summary>
    public async Task<IPendingUser> ValidateInvitationAsync( ISqlCallContext ctx, string token )
    {
        var invitation = await CheckInvitationAsync( ctx, token );
        return _pocoDir.Create<IPendingUser>( u =>
        {
            u.Email = invitation.UserTargetAddress;
            u.DefaultXLCID = invitation.CultureId;
        } );
    }

    /// <summary>
    /// Completes a registration: creates the user, sets names/password, joins the invitation groups,
    /// sets the preferred workspace then destroys the invitation.
    /// </summary>
    public async Task CompleteRegistrationAsync( ISqlCallContext ctx,
                                                 string firstName,
                                                 string lastName,
                                                 string email,
                                                 string token,
                                                 string password,
                                                 string cultureName )
    {
        var invitation = await CheckInvitationAsync( ctx, token );

        var userId = await _userPackage.CreateUserAsync( ctx, SystemActorId, email, cultureName );
        if( userId <= 0 )
        {
            ctx.Monitor.Warn( $"User already exists. (UserName: {email})" );
            throw new ArgumentException( "User.InvitationError" );
        }
        ctx.Monitor.Info( $"User created. (UserId: {userId}, UserName: {email})" );

        await _emailTable.AddEMailAsync( ctx, SystemActorId, userId, email, isPrimary: true );
        await _namedUserTable.SetNamesAsync( ctx, SystemActorId, userId, firstName, lastName );
        await _passwordTable.CreateOrUpdatePasswordUserAsync( ctx, SystemActorId, userId, password, UCLMode.CreateOnly );

        var xlcid = NormalizedCultureInfo.EnsureNormalizedCultureInfo( cultureName ).Id;
        await _userPackage.SetExtendedCultureAsync( ctx, SystemActorId, userId, xlcid );
        ctx.Monitor.Info( $"User's extended culture set. (UserId: {userId}, CultureName: {cultureName}, XLCID: {xlcid})" );

        foreach( var g in invitation.GroupIdentifiers )
        {
            await _groupTable.AddUserAsync( ctx, SystemActorId, g, userId, autoAddUserInZone: true );
            ctx.Monitor.Trace( $"User added to group. (UserId: {userId}, GroupId: {g})" );
        }

        var workspaceId = await _queries.GetWorkspaceIdForGroupsAsync( ctx, invitation.GroupIdentifiers );
        if( workspaceId > 0 )
        {
            await _workspacePackage.SetUserPreferredWorkspaceAsync( ctx, SystemActorId, userId, workspaceId );
            ctx.Monitor.Trace( $"Preferred workspace set. (UserId: {userId}, WorkspaceId: {workspaceId})" );
        }

        // The registration flow is anonymous: destroy on behalf of the invitation's original creator
        // because CK.sUserInvitationDestroy only allows the creator to delete it.
        await DestroyInvitationAsync( ctx, invitation.CreatedById, invitation.InvitationId );
        ctx.Monitor.Info( $"Invitation finalized and destroyed. (InvitationId: {invitation.InvitationId})" );
    }

    /// <summary>
    /// Resolves and validates an invitation from its secret. Mirrors the previous TokenStore check:
    /// the invitation must exist, be active and not be expired.
    /// </summary>
    async Task<IUserInvitation> CheckInvitationAsync( ISqlCallContext ctx, string token )
    {
        if( string.IsNullOrWhiteSpace( token ) ) throw new InvalidOperationException( "User.InvitationError" );

        var invitation = await _invitationPackage.GetUserInvitationAsync( ctx, Encoding.UTF8.GetBytes( token ) );
        if( invitation is null || !invitation.IsActive || invitation.ExpirationDateUtc < DateTime.UtcNow )
        {
            ctx.Monitor.Error( "Could not validate the invitation token." );
            throw new InvalidOperationException( "User.InvitationError" );
        }
        return invitation;
    }

    async Task DestroyInvitationAsync( ISqlCallContext ctx, int actorId, int invitationId )
    {
        await _invitationPackage.DestroyUserInvitationAsync( ctx, _pocoDir.Create<IDestroyUserInvitationCommand>( c =>
        {
            c.ActorId = actorId;
            c.InvitationId = invitationId;
        } ) );
    }

    /// <summary>
    /// Reads the invitation secret and dispatches the invitation e-mail with the registration link.
    /// </summary>
    async Task SendInvitationMailAsync( ISqlCallContext ctx, int invitationId, string email, string cultureName )
    {
        var secret = await _queries.GetInvitationSecretAsync( ctx, invitationId );
        if( secret is null )
        {
            ctx.Monitor.Error( $"Could not read the invitation secret. (InvitationId: {invitationId})" );
            return;
        }
        await _mailer.SendUserInvitationAsync( ctx.Monitor, email, Encoding.UTF8.GetString( secret ), cultureName );
    }
}
