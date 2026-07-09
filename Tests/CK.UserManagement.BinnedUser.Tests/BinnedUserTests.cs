using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.BinnedUser.Tests;

[TestFixture]
public class BinnedUserTests : BinnedUserTestBase
{
    [Test]
    public async Task archiving_then_restoring_a_user_toggles_its_bindate_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        var archiveCollector = new UserMessageCollector( Env.CurrentCulture );
        var archive = Env.PocoDirectory.Create<IArchiveUsersAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserIds.Add( userId );
        } );
        await Env.Handler.ArchiveUsersAsync( ctx, archiveCollector, archive, Env.BinnedUserPackage );
        archiveCollector.ErrorCount.ShouldBe( 0 );

        ( await BinDateOfAsync( ctx, userId ) ).ShouldNotBeNull();

        var restoreCollector = new UserMessageCollector( Env.CurrentCulture );
        var restore = Env.PocoDirectory.Create<IRestoreUsersAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserIds.Add( userId );
        } );
        await Env.Handler.RestoreUsersAsync( ctx, restoreCollector, restore, Env.BinnedUserPackage );
        restoreCollector.ErrorCount.ShouldBe( 0 );

        ( await BinDateOfAsync( ctx, userId ) ).ShouldBeNull();
    }

    // Reads BinDate through the BinDate-aware workspace-user listing provided by the BinnedUser handler.
    async Task<DateTime?> BinDateOfAsync( ISqlCallContext ctx, int userId )
    {
        var query = Env.PocoDirectory.Create<IGetWorkspaceUsersQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId );
        var users = await Env.Handler.GetWorkspaceUsersAsync( ctx, query, Env.BinnedUserQueries );
        return ((CK.IO.UserManagement.BinnedUser.IWorkspaceUser)users.Single( u => u.UserId == userId )).BinDate;
    }

    [Test]
    public async Task archiving_with_no_user_ids_reports_an_error_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var collector = new UserMessageCollector( Env.CurrentCulture );
        var archive = Env.PocoDirectory.Create<IArchiveUsersAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
        } );

        await Env.Handler.ArchiveUsersAsync( ctx, collector, archive, Env.BinnedUserPackage );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
    }
}
