using CK.SqlServer;
using CK.UserManagement.Testing;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

/// <summary>
/// Tests the rule <c>CK.fUserAdministrationCheck</c> directly, through <see cref="UserTable.CheckUserAdministrationAsync"/>.
/// The actor can administer a user from a workspace W when the actor is a platform administrator, or when the
/// user is a member of W, is not protected, and the actor has level 112 or more on every workspace of the user.
/// </summary>
[TestFixture]
public class UserAdministrationCheckTests : UserManagementTestBase
{
    WorkspaceScenario Scenario => new( Env.Map );

    async Task<UserAdministrationCheck> CheckAsync( ISqlCallContext ctx, int actorId, int workspaceId, int userId, bool accountTakeover = false )
        => (UserAdministrationCheck)await Env.UserTable.CheckUserAdministrationAsync( ctx, actorId, workspaceId, userId, accountTakeover );

    [Test]
    public async Task admin_of_W_can_administer_a_member_of_W_only_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.Allowed );
    }

    [Test]
    public async Task actor_zero_or_workspace_zero_or_target_System_is_invalid_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );

        ( await CheckAsync( ctx, 0, w, target ) ).ShouldBe( UserAdministrationCheck.InvalidParameters );
        ( await CheckAsync( ctx, admin, 0, target ) ).ShouldBe( UserAdministrationCheck.InvalidParameters );
        ( await CheckAsync( ctx, admin, w, 1 ) ).ShouldBe( UserAdministrationCheck.InvalidParameters );
    }

    [Test]
    public async Task a_user_outside_W_is_refused_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.NotAWorkspaceMember );
    }

    [Test]
    public async Task a_user_in_no_workspace_is_refused_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.NotAWorkspaceMember );
    }

    [Test]
    public async Task a_platform_admin_member_of_W_is_protected_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );
        await Scenario.MakePlatformAdminAsync( ctx, target );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.ProtectedUser );
    }

    [Test]
    public async Task a_System_group_member_of_W_is_protected_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );
        await Scenario.MakeSystemMemberAsync( ctx, target );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.ProtectedUser );
    }

    [Test]
    public async Task a_user_shared_with_an_unadministered_workspace_is_refused_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (w2, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w, w2 );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.UnadministeredWorkspace );
    }

    [Test]
    public async Task a_user_shared_with_an_administered_workspace_is_allowed_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (w2, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        await Scenario.GrantWorkspaceAdminAsync( ctx, w2, admin );
        int target = await Scenario.NewUserAsync( ctx, w, w2 );

        ( await CheckAsync( ctx, admin, w, target ) ).ShouldBe( UserAdministrationCheck.Allowed );
    }

    [Test]
    public async Task a_platform_admin_actor_can_administer_anyone_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int platformAdmin = await Scenario.NewUserAsync( ctx );
        await Scenario.MakePlatformAdminAsync( ctx, platformAdmin );
        int target = await Scenario.NewUserAsync( ctx, other );

        ( await CheckAsync( ctx, platformAdmin, w, target ) ).ShouldBe( UserAdministrationCheck.Allowed );
    }

    [Test]
    public async Task a_member_of_W_only_is_refused_to_a_plain_member_actor_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int plainMember = await Scenario.NewUserAsync( ctx, w );
        int target = await Scenario.NewUserAsync( ctx, w );

        ( await CheckAsync( ctx, plainMember, w, target ) ).ShouldBe( UserAdministrationCheck.UnadministeredWorkspace );
    }

    [Test]
    public async Task an_actor_does_not_administer_itself_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );

        ( await CheckAsync( ctx, admin, w, admin ) ).ShouldBe( UserAdministrationCheck.SelfAdministration );
    }

    [Test]
    public async Task a_safe_administrator_cannot_administer_a_higher_administrator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int safeAdmin = await Scenario.NewUserAsync( ctx, w );
        await Scenario.GrantWorkspaceLevelAsync( ctx, w, safeAdmin, 112 );

        ( await CheckAsync( ctx, safeAdmin, w, admin ) ).ShouldBe( UserAdministrationCheck.HigherLevelUser );
        ( await CheckAsync( ctx, admin, w, safeAdmin ) ).ShouldBe( UserAdministrationCheck.Allowed, "The other way round." );
    }

    [Test]
    public async Task only_a_platform_administrator_takes_over_the_account_of_an_administrator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int peer = await Scenario.NewUserAsync( ctx, w );
        await Scenario.GrantWorkspaceAdminAsync( ctx, w, peer );
        int member = await Scenario.NewUserAsync( ctx, w );

        ( await CheckAsync( ctx, admin, w, peer ) ).ShouldBe( UserAdministrationCheck.Allowed, "Peers administer each other." );
        ( await CheckAsync( ctx, admin, w, peer, accountTakeover: true ) ).ShouldBe( UserAdministrationCheck.ProtectedAccount );
        ( await CheckAsync( ctx, admin, w, member, accountTakeover: true ) ).ShouldBe( UserAdministrationCheck.Allowed, "The account of a member." );

        int platformAdmin = await Scenario.NewUserAsync( ctx );
        await Scenario.MakePlatformAdminAsync( ctx, platformAdmin );
        ( await CheckAsync( ctx, platformAdmin, w, peer, accountTakeover: true ) ).ShouldBe( UserAdministrationCheck.Allowed );
    }
}
