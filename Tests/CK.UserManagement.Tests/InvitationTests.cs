using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

[TestFixture]
public class InvitationTests : UserManagementTestBase
{
    ICreateInvitationCommand NewCreateInvitation( string email )
        => Env.PocoDirectory.Create<ICreateInvitationCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Email = email;
            c.CultureName = "fr";
            c.Groups.Add( Env.WorkspaceGroupId );
        } );

    [Test]
    public async Task creating_an_invitation_succeeds_persists_it_and_sends_the_email_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();

        var result = await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );

        result.Level.ShouldBe( UserMessageLevel.Info );
        Env.Mailer.Sent.ShouldContain( s => s.Destination == email );
        ( await Env.Queries.GetInvitationByEmailAsync( ctx, email ) ).ShouldNotBeNull();
    }

    [Test]
    public async Task creating_a_duplicate_invitation_returns_an_error_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();

        ( await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service ) )
            .Level.ShouldBe( UserMessageLevel.Info );

        var second = await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );
        second.Level.ShouldBe( UserMessageLevel.Error );
    }

    [Test]
    public async Task pending_invitations_are_listed_for_workspace_and_platform_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );

        var platform = await Env.Handler.GetPlatformPendingInvitationsAsync(
            ctx, Env.PocoDirectory.Create<IGetPlatformPendingInvitationsQCommand>(), Env.Queries );
        platform.ShouldContain( i => i.Email == email );

        var workspace = await Env.Handler.GetWorkspacePendingInvitationsAsync(
            ctx,
            Env.PocoDirectory.Create<IGetWorkspacePendingInvitationsQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId ),
            Env.Queries );
        workspace.ShouldContain( i => i.Email == email );
    }

    [Test]
    public async Task resending_an_invitation_dispatches_the_email_again_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );
        int before = Env.Mailer.Sent.Count( s => s.Destination == email );

        var resend = Env.PocoDirectory.Create<IResendInvitationsCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Invitations.Add( Env.PocoDirectory.Create<IPendingInvitation>( p =>
            {
                p.Email = email;
                p.CultureName = "fr";
            } ) );
        } );

        var result = await Env.Handler.ResendInvitationsAsync( ctx, resend, Env.UserTable, Env.Service );

        result.Level.ShouldBe( UserMessageLevel.Info );
        Env.Mailer.Sent.Count( s => s.Destination == email ).ShouldBeGreaterThan( before );
    }
}
