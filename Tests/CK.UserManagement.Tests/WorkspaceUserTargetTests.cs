using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement.Testing;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

/// <summary>
/// End-to-end tests of the target-user rule on the core commands: the command goes through the simulated
/// CRIS validators, then through the handler when no validator refused it.
/// </summary>
[TestFixture]
public class WorkspaceUserTargetTests : UserManagementTestBase
{
    WorkspaceScenario Scenario => new( Env.Map );

    async Task<(CrisOutcome Outcome, bool Changed)> ForceResetAsync( ISqlCallContext ctx, int actorId, int workspaceId, int target )
    {
        await Env.UserPasswordTable.CreateOrUpdatePasswordUserAsync( ctx, 1, target, "Initial.Pass.1234" );
        var before = await Scenario.PasswordHashOfAsync( ctx, target );
        before.ShouldNotBeNull();
        var cmd = Env.PocoDirectory.Create<IForceResetUserPasswordCommand>( c =>
        {
            c.ActorId = actorId;
            c.CurrentWorkspaceId = workspaceId;
            c.UserId = target;
            c.Password = "Known.1234";
        } );
        var outcome = await Env.Pipeline.RunAsync( ctx, cmd, () => Env.Handler.ForceResetUserPasswordAsync( ctx, cmd, Env.UserPasswordResetTable ) );
        var after = await Scenario.PasswordHashOfAsync( ctx, target );
        return (outcome, !before.AsSpan().SequenceEqual( after ));
    }

    [Test]
    public async Task a_workspace_admin_cannot_reset_the_password_of_a_user_outside_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );

        var (outcome, changed) = await ForceResetAsync( ctx, admin, w, target );

        changed.ShouldBeFalse();
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_reset_the_password_of_a_platform_admin_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );
        await Scenario.MakePlatformAdminAsync( ctx, target );

        var (outcome, changed) = await ForceResetAsync( ctx, admin, w, target );

        changed.ShouldBeFalse();
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_reset_the_password_of_a_user_shared_with_an_unadministered_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (w2, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w, w2 );

        var (outcome, changed) = await ForceResetAsync( ctx, admin, w, target );

        changed.ShouldBeFalse();
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_rename_a_user_outside_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );
        var originalName = await Scenario.UserNameOfAsync( ctx, target );

        var cmd = Env.PocoDirectory.Create<IEditWorkspaceUserCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.UserId = target;
            c.UserName = $"Renamed-{Guid.NewGuid():N}".Substring( 0, 24 );
            c.FirstName = "Renamed";
            c.LastName = "ByOutsider";
        } );
        var outcome = await Env.Pipeline.RunAsync( ctx, cmd,
            () => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.GroupTable,
                                                      Env.PreferredCulturePackage, Env.Queries ) );

        ( await Scenario.UserNameOfAsync( ctx, target ) ).ShouldBe( originalName );
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_can_reset_the_password_of_a_member_of_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );

        var (outcome, changed) = await ForceResetAsync( ctx, admin, w, target );

        outcome.Refused.ShouldBeFalse();
        changed.ShouldBeTrue();
        ( await Env.IsTemporaryPasswordAsync( ctx, target ) ).ShouldBeTrue();
    }

    [Test]
    public async Task a_platform_admin_can_reset_the_password_of_a_user_outside_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int platformAdmin = await Scenario.NewUserAsync( ctx );
        await Scenario.MakePlatformAdminAsync( ctx, platformAdmin );
        int target = await Scenario.NewUserAsync( ctx, other );

        var (outcome, changed) = await ForceResetAsync( ctx, platformAdmin, w, target );

        outcome.Refused.ShouldBeFalse();
        changed.ShouldBeTrue();
    }
}
