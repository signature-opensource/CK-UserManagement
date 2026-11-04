using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement.Testing;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

/// <summary>
/// End-to-end tests of the groups rule on the core commands: each group of the command must be the current
/// workspace or a group of the current workspace.
/// </summary>
[TestFixture]
public class WorkspaceGroupsTests : UserManagementTestBase
{
    WorkspaceScenario Scenario => new( Env.Map );

    async Task<CrisOutcome> EditGroupsAsync( ISqlTransactionCallContext ctx, int admin, int w, int target, params int[] groups )
    {
        var userName = await Scenario.UserNameOfAsync( ctx, target );
        var cmd = Env.PocoDirectory.Create<IEditWorkspaceUserCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.UserId = target;
            c.UserName = userName;
            c.FirstName = "Group";
            c.LastName = "Test";
            c.Groups.AddRange( groups );
        } );
        return await Env.Pipeline.RunAsync( ctx, cmd,
            () => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.GroupTable,
                                                      Env.PreferredCulturePackage, Env.Queries ) );
    }

    [Test]
    public async Task a_workspace_admin_cannot_add_a_user_to_the_platform_admin_group_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );

        var outcome = await EditGroupsAsync( ctx, admin, w, target, WorkspaceScenario.PlatformAdminGroupId );

        ( await Scenario.IsMemberAsync( ctx, target, WorkspaceScenario.PlatformAdminGroupId ) ).ShouldBeFalse();
        outcome.ErrorResNames.ShouldContain( "Admin.GroupOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_create_a_user_in_the_platform_admin_group_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var newName = $"ScopeNew-{Guid.NewGuid():N}".Substring( 0, 24 );
        var cmd = Env.PocoDirectory.Create<ICreateWorkspaceUserCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.UserName = newName;
            c.FirstName = "New";
            c.LastName = "User";
            c.Password = "Known.Pass.1234";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( WorkspaceScenario.PlatformAdminGroupId );
        } );

        var outcome = await Env.Pipeline.RunAsync( ctx, cmd,
            () => Env.Handler.CreateWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.GroupTable,
                                                        Env.PreferredCulturePackage, Env.UserPasswordResetTable, Env.WorkspacePackage ) );

        ( await Env.UserTable.FindByNameAsync( ctx, newName ) ).ShouldBe( 0 );
        outcome.ErrorResNames.ShouldContain( "Admin.GroupOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_add_a_user_to_a_group_of_another_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int otherGroup = await Scenario.NewGroupAsync( ctx, other );
        int target = await Scenario.NewUserAsync( ctx, w );

        var outcome = await EditGroupsAsync( ctx, admin, w, target, otherGroup );

        ( await Scenario.IsMemberAsync( ctx, target, otherGroup ) ).ShouldBeFalse();
        outcome.ErrorResNames.ShouldContain( "Admin.GroupOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_can_set_the_groups_of_its_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int group = await Scenario.NewGroupAsync( ctx, w );
        int target = await Scenario.NewUserAsync( ctx, w );

        var outcome = await EditGroupsAsync( ctx, admin, w, target, group );

        outcome.Refused.ShouldBeFalse();
        ( await Scenario.IsMemberAsync( ctx, target, group ) ).ShouldBeTrue();
    }

    [Test]
    public async Task an_empty_group_list_is_accepted_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );

        var outcome = await EditGroupsAsync( ctx, admin, w, target );

        outcome.Refused.ShouldBeFalse();
    }
}
