using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using Dapper;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.UserInvitation.Tests;

[TestFixture]
public class InvitationTests : UserInvitationTestBase
{
    ICreateInvitationCommand NewCreateInvitation( string email )
        => Env.PocoDirectory.Create<ICreateInvitationCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Email = email;
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
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
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) ).ShouldNotBeNull();
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
    public async Task an_expired_invitation_is_replaced_by_a_new_invitation_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        ( await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service ) )
            .Level.ShouldBe( UserMessageLevel.Info );
        var expired = await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email );
        await ctx[Env.UserTable].ExecuteAsync( "update CK.tUserInvitation set ExpirationDateUtc = @D where InvitationId = @I;",
                                               new { D = DateTime.UtcNow.AddDays( -1 ), I = expired!.InvitationId } );
        int sentBefore = Env.Mailer.Sent.Count( s => s.Destination == email );

        var second = await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );

        second.Level.ShouldBe( UserMessageLevel.Info );
        var replacement = await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email );
        replacement.ShouldNotBeNull();
        replacement.InvitationId.ShouldNotBe( expired.InvitationId );
        replacement.ExpirationDateUtc.ShouldBeGreaterThan( DateTime.UtcNow );
        Env.Mailer.Sent.Count( s => s.Destination == email ).ShouldBe( sentBefore + 1 );
    }

    [Test]
    public async Task pending_invitations_are_listed_for_workspace_and_platform_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );

        var platform = await Env.Handler.GetPlatformPendingInvitationsAsync(
            ctx, Env.PocoDirectory.Create<IGetPlatformPendingInvitationsQCommand>(), Env.InvitationQueries );
        platform.ShouldContain( i => i.Email == email );

        var workspace = await Env.Handler.GetWorkspacePendingInvitationsAsync(
            ctx,
            Env.PocoDirectory.Create<IGetWorkspacePendingInvitationsQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId ),
            Env.InvitationQueries );
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
                p.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            } ) );
        } );

        var result = await Env.Handler.ResendInvitationsAsync( ctx, resend, Env.UserTable, Env.Service );

        result.Level.ShouldBe( UserMessageLevel.Info );
        Env.Mailer.Sent.Count( s => s.Destination == email ).ShouldBeGreaterThan( before );
    }

    [Test]
    public async Task deactivating_an_invitation_marks_it_inactive_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) )!.IsActive.ShouldBeTrue();

        var deactivate = Env.PocoDirectory.Create<IDeactivateInvitationsCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Invitations.Add( Env.PocoDirectory.Create<IPendingInvitation>( p =>
            {
                p.Email = email;
            } ) );
        } );

        var result = await Env.Handler.DeactivateInvitationsAsync( ctx, deactivate, Env.UserTable, Env.Service );

        result.Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) )!.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task destroying_an_invitation_removes_it_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) ).ShouldNotBeNull();

        var destroy = Env.PocoDirectory.Create<IDestroyInvitationsCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Invitations.Add( Env.PocoDirectory.Create<IPendingInvitation>( p =>
            {
                p.Email = email;
            } ) );
        } );

        var result = await Env.Handler.DestroyInvitationsAsync( ctx, destroy, Env.UserTable, Env.Service );

        result.Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) ).ShouldBeNull();
    }
}
