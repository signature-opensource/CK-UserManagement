using System.Text;
using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

[TestFixture]
public class RegistrationTests : UserManagementTestBase
{
    [Test]
    public async Task validating_an_invalid_token_returns_an_error_message_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var cmd = Env.PocoDirectory.Create<IValidateInvitationTokenCommand>( c => c.Token = "not-a-valid-token" );

        var result = await Env.Handler.ValidateInvitationTokenAsync( ctx, cmd, Env.Service );

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
        await Env.Handler.CreateInvitationAsync( ctx, create, Env.UserTable, Env.Service );

        var invitation = await Env.Queries.GetInvitationByEmailAsync( ctx, email );
        invitation.ShouldNotBeNull();
        var secret = await Env.Queries.GetInvitationSecretAsync( ctx, invitation!.InvitationId );
        secret.ShouldNotBeNull();
        var token = Encoding.UTF8.GetString( secret! );

        // The token validates and resolves the pending user.
        var validate = Env.PocoDirectory.Create<IValidateInvitationTokenCommand>( c => c.Token = token );
        var validateResult = await Env.Handler.ValidateInvitationTokenAsync( ctx, validate, Env.Service );
        validateResult.User.ShouldNotBeNull();
        validateResult.User!.Email.ShouldBe( email );

        // Completing the registration creates the user and destroys the invitation.
        var complete = Env.PocoDirectory.Create<ICompleteRegistrationCommand>( c =>
        {
            c.Email = email;
            c.FirstName = "New";
            c.LastName = "User";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Password = "Password123!";
            c.Token = token;
        } );
        var completeResult = await Env.Handler.CompleteRegistrationAsync( ctx, complete, Env.UserTable, Env.Service );

        completeResult.Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.Queries.GetInvitationByEmailAsync( ctx, email ) ).ShouldBeNull();
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

        // The query surfaces the real user name AND the primary e-mail.
        var users = await Env.Queries.GetWorkspaceUsersAsync( ctx, Env.WorkspaceId );
        var created = users.Single( u => u.UserName == userName );
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
            Env.Service.CompleteRegistrationAsync( ctx, "New", "User", email, token, "Password123!", TestEnv.FrenchExtendedCultureId, userName ) );
        ex.Message.ShouldBe( "User.UserNameAlreadyTaken" );
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
        await Env.Handler.CreateInvitationAsync( ctx, create, Env.UserTable, Env.Service );

        var invitation = await Env.Queries.GetInvitationByEmailAsync( ctx, email );
        var secret = await Env.Queries.GetInvitationSecretAsync( ctx, invitation!.InvitationId );
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
            c.Email = email;
            c.FirstName = "New";
            c.LastName = "User";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Password = "Password123!";
            c.Token = token;
            c.UserName = userName;
        } );
        var result = await Env.Handler.CompleteRegistrationAsync( ctx, complete, Env.UserTable, Env.Service );
        return (email, result);
    }
}
