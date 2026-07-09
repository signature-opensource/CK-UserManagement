using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.UserInvitation.Tests;

[TestFixture]
public class InvitationDataTests : UserInvitationTestBase
{
    [Test]
    public async Task invitation_data_query_exposes_the_workspace_groups_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var query = Env.PocoDirectory.Create<IGetWorkspaceInvitationDataQCommand>( c => c.CurrentWorkspaceId = Env.WorkspaceId );

        var data = await Env.Handler.GetWorkspaceInvitationDataAsync( ctx, query, Env.Queries );

        data.Groups.ShouldContain( g => g.GroupId == Env.WorkspaceGroupId );
    }
}
