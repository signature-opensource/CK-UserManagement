using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement.Testing;
using Dapper;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.UserBanned.Tests;

/// <summary>
/// Tests the target-user rule on the ban and unban commands, through the simulated CRIS validators and
/// directly on the transformed stored procedures.
/// </summary>
[TestFixture]
public class BanScopeTests : UserBannedTestBase
{
    WorkspaceScenario Scenario => new( Env.Map );

    async Task<int> BanCountAsync( ISqlCallContext ctx, int userId, string keyReason )
        => await ctx[Env.UserTable].QuerySingleAsync<int>( "select count(*) from CK.tUserBanned where UserId = @U and KeyReason = @K;",
                                                           new { U = userId, K = keyReason } );

    async Task<CrisOutcome> BanThroughCrisAsync( ISqlCallContext ctx, int actorId, int workspaceId, int target )
    {
        var cmd = Env.PocoDirectory.Create<ISetUserBannedAdminCommand>( c =>
        {
            c.ActorId = actorId;
            c.CurrentWorkspaceId = workspaceId;
            c.UserId = target;
            c.KeyReason = UserBannedPackage.AdminKeyReason;
        } );
        return await Env.Pipeline.RunAsync( ctx, cmd, async () =>
        {
            var r = await Env.Handler.SetUserBannedAsync( ctx, new UserMessageCollector( Env.CurrentCulture ), cmd, Env.UserBannedPackage );
            return string.Join( " | ", r.UserMessages.Select( m => m.ResName ) );
        } );
    }

    [Test]
    public async Task a_workspace_admin_cannot_ban_a_user_outside_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );

        var outcome = await BanThroughCrisAsync( ctx, admin, w, target );

        ( await BanCountAsync( ctx, target, UserBannedPackage.AdminKeyReason ) ).ShouldBe( 0 );
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_ban_a_platform_admin_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );
        await Scenario.MakePlatformAdminAsync( ctx, target );

        var outcome = await BanThroughCrisAsync( ctx, admin, w, target );

        ( await BanCountAsync( ctx, target, UserBannedPackage.AdminKeyReason ) ).ShouldBe( 0 );
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task a_workspace_admin_cannot_remove_a_ban_of_a_user_outside_the_workspace_Async()
    {
        const string lockout = "UserPassword.TooManyAttempt";
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );
        await Env.UserBannedPackage.SetUserBannedAsync( ctx, 1, lockout, target, null, null, 0 );

        var cmd = Env.PocoDirectory.Create<IDestroyUserBannedAdminCommand>( c =>
        {
            c.ActorId = admin;
            c.CurrentWorkspaceId = w;
            c.UserId = target;
            c.KeyReason = lockout;
        } );
        var outcome = await Env.Pipeline.RunAsync( ctx, cmd, async () =>
        {
            var r = await Env.Handler.DestroyUserBannedAsync( ctx, new UserMessageCollector( Env.CurrentCulture ), cmd, Env.UserBannedPackage );
            return string.Join( " | ", r.UserMessages.Select( m => m.ResName ) );
        } );

        ( await BanCountAsync( ctx, target, lockout ) ).ShouldBe( 1 );
        outcome.ErrorResNames.ShouldContain( "Admin.UserOutOfScope" );
    }

    [Test]
    public async Task the_ban_procedure_refuses_a_user_outside_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        var (other, _) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, other );

        var e = await Should.ThrowAsync<SqlDetailedException>(
            () => Env.UserBannedPackage.SetUserBannedAsync( ctx, admin, UserBannedPackage.AdminKeyReason, target, null, null, w ) );

        e.ToString().ShouldContain( "Security.AdminOnly" );
        ( await BanCountAsync( ctx, target, UserBannedPackage.AdminKeyReason ) ).ShouldBe( 0 );
    }

    [Test]
    public async Task the_ban_procedure_accepts_a_member_of_the_workspace_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var (w, admin) = await Scenario.NewWorkspaceWithAdminAsync( ctx );
        int target = await Scenario.NewUserAsync( ctx, w );

        await Env.UserBannedPackage.SetUserBannedAsync( ctx, admin, UserBannedPackage.AdminKeyReason, target, null, null, w );

        ( await BanCountAsync( ctx, target, UserBannedPackage.AdminKeyReason ) ).ShouldBe( 1 );
    }
}
