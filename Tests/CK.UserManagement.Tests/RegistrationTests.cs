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
            c.CultureName = "fr";
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
            c.CultureName = "fr";
            c.Password = "Password123!";
            c.Token = token;
        } );
        var completeResult = await Env.Handler.CompleteRegistrationAsync( ctx, complete, Env.UserTable, Env.Service );

        completeResult.Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.Queries.GetInvitationByEmailAsync( ctx, email ) ).ShouldBeNull();
        ( await Env.UserTable.FindByNameAsync( ctx, email ) ).ShouldBeGreaterThan( 0 );
    }
}
