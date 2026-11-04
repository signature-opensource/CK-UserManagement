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

    Task<CrisOutcome> EditGroupsAsync( ISqlTransactionCallContext ctx, int admin, int w, int target, params int[] groups )
        => EditGroupsAsync( Env.Pipeline, ctx, admin, w, target, groups );

    async Task<CrisOutcome> EditGroupsAsync( CrisValidationPipeline pipeline, ISqlTransactionCallContext ctx, int admin, int w, int target, params int[] groups )
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
        return await pipeline.RunAsync( ctx, cmd,
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

    [Test]
    public async Task a_safe_administrator_cannot_give_the_administrators_group_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int safeAdmin = await Scenario.NewUserAsync( ctx, w );
        await Scenario.GrantWorkspaceLevelAsync( ctx, w, safeAdmin, 112 );
        int target = await Scenario.NewUserAsync( ctx, w );
        int administrators = await Scenario.AdministratorsGroupOfAsync( ctx, w );

        var outcome = await EditGroupsAsync( ctx, safeAdmin, w, target, administrators );

        ( await Scenario.IsMemberAsync( ctx, target, administrators ) ).ShouldBeFalse();
        outcome.ErrorResNames.ShouldBe( ["Admin.GroupAboveActorLevel"] );
    }

    [Test]
    public async Task an_administrator_can_give_the_administrators_group_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );
        int administrators = await Scenario.AdministratorsGroupOfAsync( ctx, w );

        var outcome = await EditGroupsAsync( ctx, admin, w, target, administrators );

        outcome.Refused.ShouldBeFalse();
        ( await Scenario.IsMemberAsync( ctx, target, administrators ) ).ShouldBeTrue();
    }

    // An application that hides a group of the forms.
    sealed class HidingQueries : UserManagementQueries
    {
        readonly int _hidden;

        public HidingQueries( UserTable userTable, int hidden ) : base( userTable ) => _hidden = hidden;

        public override async Task<IReadOnlyList<CK.IO.UserProfile.Workspace.IGroupInfos>> GetWorkspaceGroupsAsync( ISqlCallContext ctx, int workspaceId )
            => ( await base.GetWorkspaceGroupsAsync( ctx, workspaceId ) ).Where( g => g.GroupId != _hidden ).ToList();
    }

    [Test]
    public async Task a_group_hidden_by_the_application_is_refused_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );
        int hidden = await Scenario.NewGroupAsync( ctx, w );
        var actorLevel = new ActorWorkspaceLevel( Env.Map.StObjs.Obtain<CK.DB.Workspace.WorkspaceTable>()! );
        var pipeline = new CrisValidationPipeline( Env.CurrentCulture )
                        .RegisterAdminValidator( new AdminCommandValidator( Env.UserTable, actorLevel, Env.CurrentCulture ) )
                        .RegisterWorkspaceScopeValidator( new WorkspaceScopeValidator( Env.UserTable, actorLevel, new HidingQueries( Env.UserTable, hidden ), Env.CurrentCulture ) );

        var outcome = await EditGroupsAsync( pipeline, ctx, admin, w, target, hidden );

        ( await Scenario.IsMemberAsync( ctx, target, hidden ) ).ShouldBeFalse();
        outcome.ErrorResNames.ShouldBe( ["Admin.GroupOutOfScope"] );
    }
}
