using System.Text;
using CK.Core;
using CK.DB.Actor.ActorEMail;
using CK.DB.AppIdentity;
using CK.DB.Auth;
using CK.DB.User.NamedUser;
using CK.DB.User.UserPassword;
using CK.DB.Zone;
using CK.IO.UserInvitation;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement;

namespace CK.UserManagement.UserInvitation;

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
    readonly UserInvitationQueries _queries;
    readonly IUserManagementMailer _mailer;
    readonly PartyActorTable _partyActors;

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
                                  UserInvitationQueries queries,
                                  IUserManagementMailer mailer,
                                  PartyActorTable partyActors )
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
        _partyActors = partyActors;
    }

    // The issuing party of an invitation is recorded as an actor (CK.tUserInvitation.IssuingPartyId references CK.tActor):
    // the full name of the party is mapped to its PartyActor here, where the actors database is. Null is 0 (unbound).
    async Task<int> GetPartyActorIdAsync( ISqlCallContext ctx, string? partyFullName )
    {
        return partyFullName == null ? 0 : await _partyActors.EnsureAsync( ctx, SystemActorId, partyFullName );
    }

    /// <summary>
    /// Creates the invitation for an e-mail and sends the invitation e-mail, returning the
    /// <see cref="SimpleUserMessage"/> describing the outcome. Because <c>UserTargetAddress</c> is
    /// unique platform-wide, an e-mail can only have a single pending invitation: when one already exists,
    /// expired or deactivated included, the invitation is left untouched and an error message is returned (use
    /// <see cref="ResendInvitationAsync"/> to re-activate it instead). The pending lists show expired and deactivated
    /// invitations, so an administrator can resend or destroy them.
    /// <para>
    /// The invitation is bound to <paramref name="issuingPartyFullName"/> (the caller party of the request, null: unbound): its link targets the
    /// client application of this party, and it is redeemed only through it. When the party has no client application
    /// URL, the invitation is created but its mail is not sent: an error message says so (resend it once the party states
    /// its URL).
    /// </para>
    /// </summary>
    public async Task<SimpleUserMessage> CreateInvitationAsync( ISqlTransactionCallContext ctx, int actorId, int workspaceId, string email, int extendedCultureId, IReadOnlyList<int> groups, string? issuingPartyFullName )
    {
        var existing = await _queries.GetInvitationByEmailAsync( ctx, email );
        if( existing is not null )
        {
            ctx.Monitor.Warn( $"An invitation already exists for this e-mail. (Email: {email}, InvitationId: {existing.InvitationId})" );
            return _currentCulture.ErrorMessage( "An invitation already exists for this e-mail address.", "User.InvitationAlreadyExists" );
        }

        var create = _pocoDir.Create<ICreateUserInvitationCommand>( c =>
        {
            c.ActorId = actorId;
            c.UserTargetAddress = email;
            c.ExpirationDateUtc = DateTime.UtcNow.AddDays( 3 );
            c.IsActive = true;
            c.CultureId = extendedCultureId;
            foreach( var g in groups.Where( g => g > 0 ) ) c.GroupIdentifiers.Add( g );
        } );
        var invitation = await _invitationPackage.CreateUserInvitationAsync( ctx, create, await GetPartyActorIdAsync( ctx, issuingPartyFullName ) );

        ctx.Monitor.Info( $"Invitation created. (Email: {email}, WorkspaceId: {workspaceId}, InvitationId: {invitation.InvitationId}, IssuingParty: {issuingPartyFullName})" );
        if( !await SendInvitationMailAsync( ctx, invitation.InvitationId, email, extendedCultureId, issuingPartyFullName ) )
        {
            return _currentCulture.ErrorMessage( "The invitation is created, but its e-mail could not be sent.", "User.InvitationMailNotSent" );
        }
        return _currentCulture.InfoMessage( "Invitation successfully created.", "CrisSuccess.InvitationCreated" );
    }

    /// <summary>
    /// Re-activates a pending invitation (extends its expiration), binds it to <paramref name="issuingPartyFullName"/> (the
    /// caller party of the request) and resends the e-mail in the culture stored in the invitation.
    /// </summary>
    /// <returns>False when the mail could not be sent (no client application URL for the party), true otherwise.</returns>
    public async Task<bool> ResendInvitationAsync( ISqlCallContext ctx, int actorId, string email, string? issuingPartyFullName )
    {
        var invitation = await _queries.GetInvitationByEmailAsync( ctx, email );
        if( invitation is null )
        {
            ctx.Monitor.Warn( $"No pending invitation to resend. (Email: {email})" );
            return true;
        }
        await _invitationPackage.SetUserInvitationIssuingPartyAsync( ctx, actorId, invitation.InvitationId, await GetPartyActorIdAsync( ctx, issuingPartyFullName ) );

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
        return await SendInvitationMailAsync( ctx, invitation.InvitationId, email, invitation.CultureId, issuingPartyFullName );
    }

    /// <summary>
    /// Deactivates a pending invitation: sets it inactive so its registration link is no longer
    /// accepted by <see cref="CheckInvitationAsync"/>. The invitation record is kept (it remains
    /// listed and can be re-activated through <see cref="ResendInvitationAsync"/>).
    /// </summary>
    public async Task DeactivateInvitationAsync( ISqlCallContext ctx, int actorId, string email )
    {
        var invitation = await _queries.GetInvitationByEmailAsync( ctx, email );
        if( invitation is null )
        {
            ctx.Monitor.Warn( $"No pending invitation to deactivate. (Email: {email})" );
            return;
        }

        await _invitationPackage.SetUserInvitationIsActiveAsync( ctx, _pocoDir.Create<ISetUserInvitationIsActiveCommand>( c =>
        {
            c.ActorId = actorId;
            c.InvitationId = invitation.InvitationId;
            c.IsActive = false;
        } ) );

        ctx.Monitor.Info( $"Invitation deactivated. (Email: {email})" );
    }

    /// <summary>
    /// Completely destroys a pending invitation identified by its target e-mail. The deletion is
    /// performed on behalf of the invitation's original creator because <c>CK.sUserInvitationDestroy</c>
    /// only allows the creator to delete it.
    /// </summary>
    public async Task DestroyInvitationByEmailAsync( ISqlCallContext ctx, string email )
    {
        var invitation = await _queries.GetInvitationByEmailAsync( ctx, email );
        if( invitation is null )
        {
            ctx.Monitor.Warn( $"No pending invitation to destroy. (Email: {email})" );
            return;
        }

        await DestroyInvitationAsync( ctx, invitation.CreatedById, invitation.InvitationId );
        ctx.Monitor.Info( $"Invitation destroyed. (Email: {email})" );
    }

    /// <summary>
    /// Validates an invitation secret and returns the pending user (e-mail + default culture).
    /// </summary>
    public async Task<IPendingUser> ValidateInvitationAsync( ISqlCallContext ctx, string token, string? callerPartyFullName )
    {
        var invitation = await CheckInvitationAsync( ctx, token, callerPartyFullName );
        return _pocoDir.Create<IPendingUser>( u =>
        {
            u.Email = invitation.UserTargetAddress;
            u.DefaultXLCID = invitation.CultureId;
        } );
    }

    /// <summary>
    /// Completes a registration: creates the user, sets names/password, joins the invitation groups,
    /// sets the preferred workspace then destroys the invitation.
    /// <para>
    /// The user's e-mail is the address the invitation was sent to (the invitation's <c>UserTargetAddress</c>): the
    /// token proves that the user received it, so it is registered as the validated primary <c>CK.tActorEMail</c> and
    /// remains the identity check point. <paramref name="userName"/> is the user's nickname and is optional: when null
    /// or blank the e-mail is used as the user name.
    /// </para>
    /// </summary>
    public async Task CompleteRegistrationAsync( ISqlCallContext ctx,
                                                 string firstName,
                                                 string lastName,
                                                 string token,
                                                 string password,
                                                 int extendedCultureId,
                                                 string? callerPartyFullName,
                                                 string? userName = null )
    {
        var invitation = await CheckInvitationAsync( ctx, token, callerPartyFullName );
        var email = invitation.UserTargetAddress;

        // The user name is optional: fall back to the invited address when no nickname is provided.
        var nickName = string.IsNullOrWhiteSpace( userName ) ? email : userName.Trim();

        // When an explicit nickname is provided, reject it early with a clear message if it is already
        // taken (CK.sUserCreate would otherwise throw on the UK_CK_tUser_UserName unique constraint).
        if( !string.IsNullOrWhiteSpace( userName ) && await _userTable.FindByNameAsync( ctx, nickName ) > 0 )
        {
            ctx.Monitor.Warn( $"User name already taken. (UserName: {nickName})" );
            throw new ArgumentException( "User.UserNameAlreadyTaken" );
        }

        var userId = await _userPackage.CreateUserAsync( ctx, SystemActorId, nickName, extendedCultureId );
        if( userId <= 0 )
        {
            ctx.Monitor.Warn( $"User already exists. (UserName: {nickName})" );
            throw new ArgumentException( "User.InvitationError" );
        }
        ctx.Monitor.Info( $"User created. (UserId: {userId}, UserName: {nickName}, Email: {email}, XLCID: {extendedCultureId})" );

        await _emailTable.AddEMailAsync( ctx, SystemActorId, userId, email, isPrimary: true );
        await _emailTable.ValidateEMailAsync( ctx, SystemActorId, userId, email );

        await _namedUserTable.SetNamesAsync( ctx, SystemActorId, userId, firstName, lastName );
        await _passwordTable.CreateOrUpdatePasswordUserAsync( ctx, SystemActorId, userId, password, UCLMode.CreateOnly );

        foreach( var g in invitation.GroupIdentifiers )
        {
            await _groupTable.AddMemberAsync( ctx, SystemActorId, g, userId, autoAddMemberInZone: true );
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
    /// Resolves and validates an invitation from its secret: the invitation must exist, be active, not be expired and,
    /// when it is bound to its issuing party, be redeemed through it (<paramref name="callerPartyFullName"/>). An unbound one
    /// (created before the issuing party was recorded) is redeemed through any party.
    /// </summary>
    async Task<IUserInvitation> CheckInvitationAsync( ISqlCallContext ctx, string token, string? callerPartyFullName )
    {
        if( string.IsNullOrWhiteSpace( token ) ) throw new InvalidOperationException( "User.InvitationError" );

        var invitation = await _invitationPackage.GetUserInvitationAsync( ctx, Encoding.UTF8.GetBytes( token ) );
        if( invitation is null || !invitation.IsActive || invitation.ExpirationDateUtc < DateTime.UtcNow )
        {
            ctx.Monitor.Error( "Could not validate the invitation token." );
            throw new InvalidOperationException( "User.InvitationError" );
        }
        if( invitation.IssuingPartyId != 0 && invitation.IssuingPartyId != await GetPartyActorIdAsync( ctx, callerPartyFullName ) )
        {
            ctx.Monitor.Error( $"The invitation is redeemed through another party than the one that issued it. (InvitationId: {invitation.InvitationId}, IssuingPartyId: {invitation.IssuingPartyId}, CallerParty: {callerPartyFullName})" );
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
    /// Reads the invitation secret and dispatches the invitation e-mail with the registration link of the issuing party.
    /// </summary>
    /// <returns>False when the mail could not be sent.</returns>
    async Task<bool> SendInvitationMailAsync( ISqlCallContext ctx, int invitationId, string email, int extendedCultureId, string? issuingPartyFullName )
    {
        var secret = await _queries.GetInvitationSecretAsync( ctx, invitationId );
        if( secret is null )
        {
            ctx.Monitor.Error( $"Could not read the invitation secret. (InvitationId: {invitationId})" );
            return false;
        }
        return await _mailer.SendUserInvitationAsync( ctx.Monitor, email, Encoding.UTF8.GetString( secret ), extendedCultureId, issuingPartyFullName );
    }
}
