using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

[TestFixture]
public class QueriesTests : UserManagementTestBase
{
    [Test]
    public async Task workspace_users_query_returns_members_and_excludes_the_system_user_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var users = await Env.Queries.GetWorkspaceUsersAsync( ctx, Env.WorkspaceId );

        users.ShouldContain( u => u.UserId == Env.AdminUserId );
        users.ShouldContain( u => u.UserId == Env.MemberUserId );
        users.ShouldNotContain( u => u.UserId <= 1 );
    }

    [Test]
    public async Task workspace_users_query_flags_the_workspace_admin_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var users = await Env.Queries.GetWorkspaceUsersAsync( ctx, Env.WorkspaceId );

        users.Single( u => u.UserId == Env.AdminUserId ).IsWorkspaceAdmin.ShouldBeTrue();
        users.Single( u => u.UserId == Env.MemberUserId ).IsWorkspaceAdmin.ShouldBeFalse();
    }

    [Test]
    public async Task workspace_groups_query_returns_the_spare_group_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var groups = await Env.Queries.GetWorkspaceGroupsAsync( ctx, Env.WorkspaceId );

        groups.ShouldContain( g => g.GroupId == Env.WorkspaceGroupId );
    }

    [Test]
    public async Task edit_data_query_exposes_the_workspace_groups_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var query = Env.PocoDirectory.Create<IGetWorkspaceUserEditDataQCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = Env.MemberUserId;
        } );

        var data = await Env.Handler.GetWorkspaceUserEditDataAsync( ctx, query, Env.Queries );

        data.WorkspaceGroups.ShouldContain( g => g.GroupId == Env.WorkspaceGroupId );
    }

    [Test]
    public async Task invitation_data_query_exposes_the_workspace_groups_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var query = Env.PocoDirectory.Create<IGetWorkspaceInvitationDataQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId );

        var data = await Env.Handler.GetWorkspaceInvitationDataAsync( ctx, query, Env.Queries );

        data.Groups.ShouldContain( g => g.GroupId == Env.WorkspaceGroupId );
    }
}
