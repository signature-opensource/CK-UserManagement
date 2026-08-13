using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.UserBanned.Tests;

[TestFixture]
public class UserBannedTests : UserBannedTestBase
{
    [Test]
    public async Task banning_then_unbanning_a_user_toggles_its_ban_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        var setCollector = new UserMessageCollector( Env.CurrentCulture );
        var set = Env.PocoDirectory.Create<ISetUserBannedAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.KeyReason = UserBannedPackage.AdminKeyReason;
        } );
        await Env.Handler.SetUserBannedAsync( ctx, setCollector, set, Env.UserBannedPackage );
        setCollector.ErrorCount.ShouldBe( 0 );

        var bans = await BansOfAsync( ctx, userId );
        bans.Count.ShouldBe( 1 );
        bans[0].KeyReason.ShouldBe( UserBannedPackage.AdminKeyReason );
        // Null BanEndDate means eternal: the stored procedure stores 9999-12-31.
        bans[0].BanEndDate.ShouldBe( new DateTime( 9999, 12, 31 ) );

        var destroyCollector = new UserMessageCollector( Env.CurrentCulture );
        var destroy = Env.PocoDirectory.Create<IDestroyUserBannedAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.KeyReason = UserBannedPackage.AdminKeyReason;
        } );
        await Env.Handler.DestroyUserBannedAsync( ctx, destroyCollector, destroy, Env.UserBannedPackage );
        destroyCollector.ErrorCount.ShouldBe( 0 );

        ( await BansOfAsync( ctx, userId ) ).ShouldBeEmpty();
    }

    [Test]
    public async Task a_user_carries_one_ban_per_key_reason_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        await BanAsync( ctx, userId, UserBannedPackage.AdminKeyReason );
        await BanAsync( ctx, userId, "UserManagement.Tests.OtherReason" );

        var bans = await BansOfAsync( ctx, userId );
        bans.Select( b => b.KeyReason )
            .ShouldBe( [UserBannedPackage.AdminKeyReason, "UserManagement.Tests.OtherReason"], ignoreOrder: true );

        // Destroying one reason leaves the other in place.
        var collector = new UserMessageCollector( Env.CurrentCulture );
        var destroy = Env.PocoDirectory.Create<IDestroyUserBannedAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.KeyReason = UserBannedPackage.AdminKeyReason;
        } );
        await Env.Handler.DestroyUserBannedAsync( ctx, collector, destroy, Env.UserBannedPackage );
        collector.ErrorCount.ShouldBe( 0 );

        var remaining = await BansOfAsync( ctx, userId );
        remaining.Count.ShouldBe( 1 );
        remaining[0].KeyReason.ShouldBe( "UserManagement.Tests.OtherReason" );
    }

    [Test]
    public async Task banning_with_no_key_reason_reports_an_error_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        var collector = new UserMessageCollector( Env.CurrentCulture );
        var set = Env.PocoDirectory.Create<ISetUserBannedAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.KeyReason = "";
        } );

        await Env.Handler.SetUserBannedAsync( ctx, collector, set, Env.UserBannedPackage );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
        ( await BansOfAsync( ctx, userId ) ).ShouldBeEmpty();
    }

    [Test]
    public async Task a_workspace_member_cannot_ban_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        // MemberUserId has no SafeAdministrator grant on the workspace: the injected
        // BannedSecurityCheck sets @CanContinue to 0 and the procedure throws Security.AdminOnly.
        var collector = new UserMessageCollector( Env.CurrentCulture );
        var set = Env.PocoDirectory.Create<ISetUserBannedAdminCommand>( c =>
        {
            c.ActorId = Env.MemberUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.KeyReason = UserBannedPackage.AdminKeyReason;
        } );

        await Env.Handler.SetUserBannedAsync( ctx, collector, set, Env.UserBannedPackage );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
        ( await BansOfAsync( ctx, userId ) ).ShouldBeEmpty();
    }

    // The banishment and group joins of the ban-aware listing fan out into each other: the SQL returns
    // one row per (banishment, group) pair. Without the de-duplication guards of the projection, a user
    // with 2 banishments and 2 groups would come back with 4 of each.
    [Test]
    public async Task the_ban_aware_listing_duplicates_neither_the_bans_nor_the_groups_Async()
    {
        var groupTable = Env.Map.StObjs.Obtain<CK.DB.Zone.GroupTable>()!;
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        // Two groups: the workspace zone group itself (the mere membership) and the spare group.
        await groupTable.AddUserAsync( ctx, 1, Env.WorkspaceGroupId, userId, autoAddUserInZone: true );
        await BanAsync( ctx, userId, UserBannedPackage.AdminKeyReason );
        await BanAsync( ctx, userId, "UserManagement.Tests.OtherReason" );

        var query = Env.PocoDirectory.Create<IGetWorkspaceUsersQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId );
        var users = await Env.ListHandler.GetWorkspaceUsersAsync( ctx, query, Env.UserBannedQueries );
        var user = (CK.IO.UserManagement.UserBanned.IWorkspaceUser)users.Single( u => u.UserId == userId );

        user.Bans.Select( b => b.KeyReason )
            .ShouldBe( [UserBannedPackage.AdminKeyReason, "UserManagement.Tests.OtherReason"], ignoreOrder: true );
        user.Groups.Select( g => g.GroupId )
            .ShouldBe( [Env.WorkspaceId, Env.WorkspaceGroupId], ignoreOrder: true );
    }

    async Task BanAsync( ISqlCallContext ctx, int userId, string keyReason )
    {
        var collector = new UserMessageCollector( Env.CurrentCulture );
        var set = Env.PocoDirectory.Create<ISetUserBannedAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.KeyReason = keyReason;
        } );
        await Env.Handler.SetUserBannedAsync( ctx, collector, set, Env.UserBannedPackage );
        collector.ErrorCount.ShouldBe( 0 );
    }

    // Reads the bans through the ban-aware workspace-user listing provided by the UserBanned handler.
    async Task<IList<CK.IO.UserManagement.UserBanned.IUserBan>> BansOfAsync( ISqlCallContext ctx, int userId )
    {
        var query = Env.PocoDirectory.Create<IGetWorkspaceUsersQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId );
        var users = await Env.ListHandler.GetWorkspaceUsersAsync( ctx, query, Env.UserBannedQueries );
        return ((CK.IO.UserManagement.UserBanned.IWorkspaceUser)users.Single( u => u.UserId == userId )).Bans;
    }
}
