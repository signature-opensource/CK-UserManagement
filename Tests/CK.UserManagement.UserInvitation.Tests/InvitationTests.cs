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

    /// <summary>Creates an invitation in the test workspace, then moves its expiration date to yesterday.</summary>
    async Task<int> CreateExpiredInvitationAsync( ISqlTransactionCallContext ctx, string email )
    {
        ( await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service ) )
            .Level.ShouldBe( UserMessageLevel.Info );
        var invitation = await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email );
        await ctx[Env.UserTable].ExecuteAsync( "update CK.tUserInvitation set ExpirationDateUtc = @D where InvitationId = @I;",
                                               new { D = DateTime.UtcNow.AddDays( -1 ), I = invitation!.InvitationId } );
        return invitation.InvitationId;
    }

    async Task<(IPendingInvitation? Workspace, IPendingInvitation? Platform)> ListedAsync( ISqlCallContext ctx, string email )
    {
        var platform = await Env.Handler.GetPlatformPendingInvitationsAsync(
            ctx, Env.PocoDirectory.Create<IGetPlatformPendingInvitationsQCommand>(), Env.InvitationQueries );
        var workspace = await Env.Handler.GetWorkspacePendingInvitationsAsync(
            ctx,
            Env.PocoDirectory.Create<IGetWorkspacePendingInvitationsQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId ),
            Env.InvitationQueries );
        return (workspace.SingleOrDefault( i => i.Email == email ), platform.SingleOrDefault( i => i.Email == email ));
    }

    [Test]
    public async Task an_expired_invitation_is_listed_as_not_active_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await CreateExpiredInvitationAsync( ctx, email );

        var (workspace, platform) = await ListedAsync( ctx, email );

        workspace.ShouldNotBeNull();
        workspace.Active.ShouldBeFalse();
        platform.ShouldNotBeNull();
        platform.Active.ShouldBeFalse();
    }

    [Test]
    public async Task resending_an_expired_invitation_reactivates_it_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        await CreateExpiredInvitationAsync( ctx, email );
        var resend = Env.PocoDirectory.Create<IResendInvitationsCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.Invitations.Add( Env.PocoDirectory.Create<IPendingInvitation>( p => p.Email = email ) );
        } );

        ( await Env.Handler.ResendInvitationsAsync( ctx, resend, Env.UserTable, Env.Service ) ).Level.ShouldBe( UserMessageLevel.Info );

        var (workspace, _) = await ListedAsync( ctx, email );
        workspace.ShouldNotBeNull();
        workspace.Active.ShouldBeTrue();
        workspace.ExpirationDateUtc.ShouldBeGreaterThan( DateTime.UtcNow );
    }

    [Test]
    public async Task an_expired_invitation_is_kept_and_still_blocks_a_new_invitation_Async()
    {
        var email = TestEnv.NewEmail();
        using var ctx = new SqlTransactionCallContext();
        int expiredId = await CreateExpiredInvitationAsync( ctx, email );

        var second = await Env.Handler.CreateInvitationAsync( ctx, NewCreateInvitation( email ), Env.UserTable, Env.Service );

        second.Level.ShouldBe( UserMessageLevel.Error );
        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) )!.InvitationId.ShouldBe( expiredId );
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
