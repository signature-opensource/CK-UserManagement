using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement.Testing;
using Dapper;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.UserInvitation.Tests;

/// <summary>
/// Tests the workspace scope rules on the invitation commands, through the simulated CRIS validators.
/// </summary>
[TestFixture]
public class InvitationScopeTests : UserInvitationTestBase
{
    WorkspaceScenario Scenario => new( Env.Map );

    Task<int> InvitationCountAsync( ISqlCallContext ctx, string email )
        => ctx[Env.UserTable].QuerySingleAsync<int>( "select count(*) from CK.tUserInvitation where UserTargetAddress = @E;", new { E = email } );

    [Test]
    public async Task a_workspace_admin_cannot_invite_into_the_platform_admin_group_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var email = TestEnv.NewEmail();
        int sentBefore = Env.Mailer.Sent.Count;
        var cmd = Env.PocoDirectory.Create<ICreateInvitationCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.Email = email;
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( WorkspaceScenario.PlatformAdminGroupId );
        } );

        var outcome = await Env.Pipeline.RunAsync( ctx, cmd, () => Env.Handler.CreateInvitationAsync( ctx, cmd, Env.UserTable, Env.Service, AuthenticationInfo.None ) );

        ( await InvitationCountAsync( ctx, email ) ).ShouldBe( 0 );
        Env.Mailer.Sent.Count.ShouldBe( sentBefore );
        outcome.ErrorResNames.ShouldContain( "Admin.GroupOutOfScope" );
    }

    static int EnglishExtendedCultureId => NormalizedCultureInfo.EnsureNormalizedCultureInfo( "en" ).Id;

    IPendingInvitation Pending( string email, int? extendedCultureId = null )
        => Env.PocoDirectory.Create<IPendingInvitation>( i =>
        {
            i.Email = email;
            i.ExtendedCultureId = extendedCultureId ?? TestEnv.FrenchExtendedCultureId;
        } );

    /// <summary>Creates an invitation into a group of a new workspace A, and a second workspace B with its administrator.</summary>
    async Task<(string Email, int A, int AdminA, int B, int AdminB)> InvitationOfAAndAdminOfBAsync( ISqlTransactionCallContext ctx )
    {
        var (a, adminA) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (b, adminB) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int groupOfA = await Scenario.NewGroupAsync( ctx, a );
        var email = TestEnv.NewEmail();
        var created = await Env.Service.CreateInvitationAsync( ctx, adminA, a, email, TestEnv.FrenchExtendedCultureId, [groupOfA], issuingPartyId: 0 );
        created.Level.ShouldBe( UserMessageLevel.Info );
        return (email, a, adminA, b, adminB);
    }

    Task<CrisOutcome> DestroyAsync( ISqlTransactionCallContext ctx, int actorId, int workspaceId, string email )
    {
        var cmd = Env.PocoDirectory.Create<IDestroyInvitationsCommand>( c =>
        {
            c.ActorId = actorId;
            c.CurrentWorkspaceId = workspaceId;
            c.Invitations.Add( Pending( email ) );
        } );
        return Env.Pipeline.RunAsync( ctx, cmd, () => Env.Handler.DestroyInvitationsAsync( ctx, cmd, Env.UserTable, Env.Service ) );
    }

    Task<CrisOutcome> ResendAsync( ISqlTransactionCallContext ctx, int actorId, int workspaceId, IPendingInvitation invitation )
    {
        var cmd = Env.PocoDirectory.Create<IResendInvitationsCommand>( c =>
        {
            c.ActorId = actorId;
            c.CurrentWorkspaceId = workspaceId;
            c.Invitations.Add( invitation );
        } );
        return Env.Pipeline.RunAsync( ctx, cmd, () => Env.Handler.ResendInvitationsAsync( ctx, cmd, Env.UserTable, Env.Service, AuthenticationInfo.None ) );
    }

    [Test]
    public async Task an_admin_of_another_workspace_cannot_destroy_an_invitation_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, _, _, b, adminB) = await InvitationOfAAndAdminOfBAsync( ctx );

        var outcome = await DestroyAsync( ctx, adminB, b, email );

        ( await InvitationCountAsync( ctx, email ) ).ShouldBe( 1 );
        outcome.ErrorResNames.ShouldContain( "Admin.InvitationOutOfScope" );
    }

    [Test]
    public async Task an_admin_of_another_workspace_cannot_deactivate_an_invitation_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, _, _, b, adminB) = await InvitationOfAAndAdminOfBAsync( ctx );

        var cmd = Env.PocoDirectory.Create<IDeactivateInvitationsCommand>( c =>
        {
            c.ActorId = adminB;
            c.CurrentWorkspaceId = b;
            c.Invitations.Add( Pending( email ) );
        } );
        var outcome = await Env.Pipeline.RunAsync( ctx, cmd, () => Env.Handler.DeactivateInvitationsAsync( ctx, cmd, Env.UserTable, Env.Service ) );

        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) )!.IsActive.ShouldBeTrue();
        outcome.ErrorResNames.ShouldContain( "Admin.InvitationOutOfScope" );
    }

    [Test]
    public async Task an_admin_of_another_workspace_cannot_resend_an_invitation_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, _, adminA, b, adminB) = await InvitationOfAAndAdminOfBAsync( ctx );
        await Env.Service.DeactivateInvitationAsync( ctx, adminA, email );
        int sentBefore = Env.Mailer.Sent.Count( s => s.Destination == email );

        var outcome = await ResendAsync( ctx, adminB, b, Pending( email ) );

        ( await Env.InvitationQueries.GetInvitationByEmailAsync( ctx, email ) )!.IsActive.ShouldBeFalse();
        Env.Mailer.Sent.Count( s => s.Destination == email ).ShouldBe( sentBefore );
        outcome.ErrorResNames.ShouldContain( "Admin.InvitationOutOfScope" );
    }

    [Test]
    public async Task a_platform_admin_can_destroy_an_invitation_of_any_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, _, _, b, _) = await InvitationOfAAndAdminOfBAsync( ctx );
        int platformAdmin = await Scenario.NewUserAsync( ctx );
        await Scenario.MakePlatformAdminAsync( ctx, platformAdmin );

        var outcome = await DestroyAsync( ctx, platformAdmin, b, email );

        outcome.Refused.ShouldBeFalse();
        ( await InvitationCountAsync( ctx, email ) ).ShouldBe( 0 );
    }

    [Test]
    public async Task a_workspace_admin_cannot_destroy_an_invitation_without_groups_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (a, adminA) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var email = TestEnv.NewEmail();
        await Env.Service.CreateInvitationAsync( ctx, adminA, a, email, TestEnv.FrenchExtendedCultureId, [], issuingPartyId: 0 );

        var outcome = await DestroyAsync( ctx, adminA, a, email );

        ( await InvitationCountAsync( ctx, email ) ).ShouldBe( 1 );
        outcome.ErrorResNames.ShouldContain( "Admin.InvitationOutOfScope" );
    }

    [Test]
    public async Task resend_uses_the_culture_stored_in_the_invitation_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, a, adminA, _, _) = await InvitationOfAAndAdminOfBAsync( ctx );

        var outcome = await ResendAsync( ctx, adminA, a, Pending( email, EnglishExtendedCultureId ) );

        outcome.Refused.ShouldBeFalse();
        Env.Mailer.Sent.Last( s => s.Destination == email ).ExtendedCultureId.ShouldBe( TestEnv.FrenchExtendedCultureId );
    }

    [Test]
    public async Task a_plain_member_learns_nothing_about_an_invitation_of_another_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (email, _, _, b, _) = await InvitationOfAAndAdminOfBAsync( ctx );
        int plainMemberOfB = await Scenario.NewUserAsync( ctx, b );

        var outcome = await DestroyAsync( ctx, plainMemberOfB, b, email );

        ( await InvitationCountAsync( ctx, email ) ).ShouldBe( 1 );
        outcome.ErrorResNames.ShouldBe( ["Admin.NotAuthorized"] );
    }

    [Test]
    public async Task a_workspace_admin_cannot_change_the_email_of_a_user_outside_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );
        var originalEmail = TestEnv.NewEmail();
        await Env.EmailTable.AddEMailAsync( ctx, 1, target, originalEmail, isPrimary: true );
        var userName = await Scenario.UserNameOfAsync( ctx, target );

        var cmd = Env.PocoDirectory.Create<CK.IO.UserManagement.UserInvitation.IEditWorkspaceUserCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.UserId = target;
            c.UserName = userName;
            c.FirstName = "Taken";
            c.LastName = "Over";
            c.Email = TestEnv.NewEmail();
        } );
        var outcome = await Env.Pipeline.RunAsync( ctx, cmd,
            () => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.GroupTable,
                                                      Env.PreferredCulturePackage, Env.EmailTable, Env.Queries, Env.InvitationQueries ) );

        ( await Env.InvitationQueries.GetPrimaryEmailAsync( ctx, target ) ).ShouldBe( originalEmail );
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    async Task<CrisOutcome> EditEmailAsync( ISqlTransactionCallContext ctx, int admin, int w, int target, string email )
    {
        var userName = await Scenario.UserNameOfAsync( ctx, target );
        var cmd = Env.PocoDirectory.Create<CK.IO.UserManagement.UserInvitation.IEditWorkspaceUserCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.UserId = target;
            c.UserName = userName;
            c.FirstName = "Peer";
            c.LastName = "Admin";
            c.Email = email;
        } );
        return await Env.Pipeline.RunAsync( ctx, cmd,
            () => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.GroupTable,
                                                      Env.PreferredCulturePackage, Env.EmailTable, Env.Queries, Env.InvitationQueries ) );
    }

    [Test]
    public async Task only_a_platform_admin_changes_the_email_of_a_workspace_administrator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int peer = await Scenario.NewUserAsync( ctx, w );
        await Scenario.GrantWorkspaceAdminAsync( ctx, w, peer );
        var originalEmail = TestEnv.NewEmail();
        await Env.EmailTable.AddEMailAsync( ctx, 1, peer, originalEmail, isPrimary: true );

        var outcome = await EditEmailAsync( ctx, admin, w, peer, TestEnv.NewEmail() );

        ( await Env.InvitationQueries.GetPrimaryEmailAsync( ctx, peer ) ).ShouldBe( originalEmail );
        outcome.ErrorResNames.ShouldBe( ["Admin.ProtectedAccount"] );
    }

    [Test]
    public async Task an_administrator_edits_a_peer_when_its_email_does_not_change_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int peer = await Scenario.NewUserAsync( ctx, w );
        await Scenario.GrantWorkspaceAdminAsync( ctx, w, peer );
        var originalEmail = TestEnv.NewEmail();
        await Env.EmailTable.AddEMailAsync( ctx, 1, peer, originalEmail, isPrimary: true );

        var outcome = await EditEmailAsync( ctx, admin, w, peer, originalEmail.ToUpperInvariant() );

        outcome.Refused.ShouldBeFalse();
        ( await Env.InvitationQueries.GetPrimaryEmailAsync( ctx, peer ) ).ShouldBe( originalEmail );
    }
}
