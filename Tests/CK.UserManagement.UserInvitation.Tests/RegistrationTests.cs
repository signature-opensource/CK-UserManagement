using System.Text;
using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using Dapper;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.UserInvitation.Tests;

[TestFixture]
public class RegistrationTests : UserInvitationTestBase
{
    [Test]
    public async Task validating_an_invalid_token_returns_an_error_message_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var cmd = Env.PocoDirectory.Create<IValidateInvitationTokenCommand>( c => c.Token = "not-a-valid-token" );

        var result = await Env.Handler.ValidateInvitationTokenAsync( ctx, cmd, Env.Service, AuthenticationInfo.None );

        result.UserMessage!.Value.Level.ShouldBe( UserMessageLevel.Error );
        result.User.ShouldBeNull();
    }

    [Test]
    public async Task completing_a_registration_creates_the_user_and_consumes_the_invitation_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();

        var create = Env.PocoDirectory.Create<ICreateInvitationCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Email = email;
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( Env.WorkspaceGroupId );
        } );
        await Env.Handler.CreateInvitationAsync( ctx, create, Env.UserTable, Env.Service, AuthenticationInfo.None );

        var invitation = await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email );
        invitation.ShouldNotBeNull();
        var secret = await Env.InvitationQueries.GetInvitationSecretAsync( ctx, invitation!.InvitationId );
        secret.ShouldNotBeNull();
        var token = Encoding.UTF8.GetString( secret! );

        // The token validates and resolves the pending user.
        var validate = Env.PocoDirectory.Create<IValidateInvitationTokenCommand>( c => c.Token = token );
        var validateResult = await Env.Handler.ValidateInvitationTokenAsync( ctx, validate, Env.Service, AuthenticationInfo.None );
        validateResult.User.ShouldNotBeNull();
        validateResult.User!.Email.ShouldBe( email );

        // Completing the registration creates the user and destroys the invitation.
        var complete = Env.PocoDirectory.Create<ICompleteRegistrationCommand>( c =>
        {
            c.FirstName = "New";
            c.LastName = "User";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Password = "Password123!";
            c.Token = token;
        } );
        var completeResult = await Env.Handler.CompleteRegistrationAsync( ctx, complete, Env.UserTable, Env.Service, AuthenticationInfo.None );

        completeResult.Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) ).ShouldBeNull();
        ( await Env.UserTable.FindByNameAsync( ctx, email ) ).ShouldBeGreaterThan( 0 );
    }

    [Test]
    public async Task registering_without_a_user_name_uses_the_email_as_nickname_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, result) = await RegisterAsync( ctx, userName: null );

        result.Level.ShouldBe( UserMessageLevel.Info );
        // No nickname provided: the e-mail is used as the user name (no regression).
        ( await Env.UserTable.FindByNameAsync( ctx, email ) ).ShouldBeGreaterThan( 0 );
    }

    [Test]
    public async Task registering_with_a_user_name_keeps_it_as_nickname_and_exposes_the_email_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var userName = $"nick-{Guid.NewGuid():N}".Substring( 0, 20 );
        var (email, result) = await RegisterAsync( ctx, userName );

        result.Level.ShouldBe( UserMessageLevel.Info );
        // The nickname is the provided user name, NOT the e-mail.
        ( await Env.UserTable.FindByNameAsync( ctx, userName ) ).ShouldBeGreaterThan( 0 );
        ( await Env.UserTable.FindByNameAsync( ctx, email ) ).ShouldBe( 0 );

        // The e-mail-aware query (UserInvitation) surfaces the real user name AND the primary e-mail.
        var users = await Env.InvitationQueries.GetWorkspaceUsersWithEmailAsync( ctx, Env.WorkspaceId );
        var created = (CK.IO.UserManagement.UserInvitation.IWorkspaceUser)users.Single( u => u.UserName == userName );
        created.Email.ShouldBe( email );
    }

    [Test]
    public async Task registering_with_an_already_taken_user_name_returns_an_error_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var userName = $"nick-{Guid.NewGuid():N}".Substring( 0, 20 );

        // First registration takes the nickname.
        var (_, first) = await RegisterAsync( ctx, userName );
        first.Level.ShouldBe( UserMessageLevel.Info );

        // A second registration (new e-mail/invitation) reusing the nickname is rejected. The command
        // handler resolves the exception to a generic Error SimpleUserMessage; asserting through the
        // service directly lets us pin the exact translation key.
        var (email, token) = await CreateInvitationAsync( ctx );
        var ex = await Should.ThrowAsync<ArgumentException>(
            Env.Service.CompleteRegistrationAsync( ctx, "New", "User", token, "Password123!", TestEnv.FrenchExtendedCultureId, 0, userName ) );
        ex.Message.ShouldBe( "User.UserNameAlreadyTaken" );
    }

    [Test]
    public async Task the_registered_email_is_the_address_the_invitation_was_sent_to_Async()
    {
        // The command has no e-mail: whoever holds the link registers the invited address, and no other one
        // (it is registered as validated: the token proves its reception).
        using var ctx = new SqlTransactionCallContext();
        var (email, result) = await RegisterAsync( ctx, userName: $"nick-{Guid.NewGuid():N}".Substring( 0, 20 ) );
        result.Level.ShouldBe( UserMessageLevel.Info );

        var users = await Env.InvitationQueries.GetWorkspaceUsersWithEmailAsync( ctx, Env.WorkspaceId );
        users.Cast<CK.IO.UserManagement.UserInvitation.IWorkspaceUser>()
             .Where( u => u.Email == email )
             .ShouldHaveSingleItem();
        // The invited address is the primary one, and it is validated.
        var stored = await ctx[Env.UserTable].QuerySingleAsync<(bool IsPrimary, DateTime ValTime)>(
            "select IsPrimary, ValTime from CK.tActorEMail where EMail = @E;", new { E = email } );
        stored.IsPrimary.ShouldBeTrue();
        stored.ValTime.ShouldBeGreaterThan( new DateTime( 2000, 1, 1 ) );
    }

    [Test]
    public async Task an_invitation_is_redeemed_only_through_its_issuing_party_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var email = TestEnv.NewEmail();
        // Issued through the party #1 (an actor: the System one here).
        ( await Env.Service.CreateInvitationAsync( ctx, Env.AdminUserId, Env.WorkspaceId, email, TestEnv.FrenchExtendedCultureId, [Env.WorkspaceGroupId], issuingPartyId: 1 ) )
            .Level.ShouldBe( UserMessageLevel.Info );
        Env.Mailer.Sent.ShouldContain( s => s.Destination == email && s.IssuingPartyId == 1, "The link targets the issuing party." );
        var invitation = await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email );
        var token = Encoding.UTF8.GetString( ( await Env.InvitationQueries.GetInvitationSecretAsync( ctx, invitation!.InvitationId ) )! );

        // Through another party: refused, with the same answer as an invalid invitation.
        ( await Should.ThrowAsync<InvalidOperationException>( Env.Service.ValidateInvitationAsync( ctx, token, callerPartyId: 3712 ) ) )
            .Message.ShouldBe( "User.InvitationError" );
        // Through its party: accepted.
        ( await Env.Service.ValidateInvitationAsync( ctx, token, callerPartyId: 1 ) ).Email.ShouldBe( email );
    }

    [Test]
    public async Task an_unbound_invitation_is_redeemed_through_any_party_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        // Created before the issuing party was recorded (0: unbound).
        var (email, token) = await CreateInvitationAsync( ctx );
        ( await Env.Service.ValidateInvitationAsync( ctx, token, callerPartyId: 3712 ) ).Email.ShouldBe( email );
    }

    [Test]
    public async Task without_a_client_application_URL_for_its_party_the_invitation_mail_is_not_sent_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var email = TestEnv.NewEmail();
        Env.Mailer.HasClientUrl = false;
        try
        {
            ( await Env.Service.CreateInvitationAsync( ctx, Env.AdminUserId, Env.WorkspaceId, email, TestEnv.FrenchExtendedCultureId, [Env.WorkspaceGroupId], issuingPartyId: 0 ) )
                .Level.ShouldBe( UserMessageLevel.Error, "The invitation is created, its mail is not sent: the administrator is told." );
        }
        finally
        {
            Env.Mailer.HasClientUrl = true;
        }
        Env.Mailer.Sent.ShouldNotContain( s => s.Destination == email );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) ).ShouldNotBeNull( "It can be resent once the party states its URL." );
    }

    /// <summary>
    /// Creates an invitation (in the test workspace group) for a fresh e-mail and returns the e-mail
    /// together with its registration token.
    /// </summary>
    async Task<(string Email, string Token)> CreateInvitationAsync( ISqlTransactionCallContext ctx )
    {
        var email = TestEnv.NewEmail();

        var create = Env.PocoDirectory.Create<ICreateInvitationCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Email = email;
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( Env.WorkspaceGroupId );
        } );
        await Env.Handler.CreateInvitationAsync( ctx, create, Env.UserTable, Env.Service, AuthenticationInfo.None );

        var invitation = await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email );
        var secret = await Env.InvitationQueries.GetInvitationSecretAsync( ctx, invitation!.InvitationId );
        return (email, Encoding.UTF8.GetString( secret! ));
    }

    /// <summary>
    /// Creates an invitation for a fresh e-mail and completes the registration with the given optional
    /// <paramref name="userName"/>.
    /// </summary>
    async Task<(string Email, SimpleUserMessage Result)> RegisterAsync( ISqlTransactionCallContext ctx, string? userName )
    {
        var (email, token) = await CreateInvitationAsync( ctx );

        var complete = Env.PocoDirectory.Create<ICompleteRegistrationCommand>( c =>
        {
            c.FirstName = "New";
            c.LastName = "User";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Password = "Password123!";
            c.Token = token;
            c.UserName = userName;
        } );
        var result = await Env.Handler.CompleteRegistrationAsync( ctx, complete, Env.UserTable, Env.Service, AuthenticationInfo.None );
        return (email, result);
    }
}
