using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using Dapper;

namespace CK.UserManagement.BinnedUser;

/// <summary>
/// Dapper read queries for the BinnedUser package. The <c>BinDate</c>-aware workspace-user listing reads
/// the core columns plus <c>CK.vUser.BinDate</c> (contributed by CK.DB.User.BinnedUser's vUser transform)
/// and materializes the BinnedUser extension Poco directly (CK.SqlServer.Dapper abstract type map).
/// </summary>
public class BinnedUserQueries : IAutoService
{
    readonly BinnedUserPackage _binnedUserPackage;

    public BinnedUserQueries( BinnedUserPackage binnedUserPackage )
    {
        _binnedUserPackage = binnedUserPackage;
    }

    public async Task<IReadOnlyList<IWorkspaceUser>> GetWorkspaceUsersWithBinDateAsync( ISqlCallContext ctx, int workspaceId )
    {
        var users = await ctx[_binnedUserPackage].QueryAsync<CK.IO.UserManagement.BinnedUser.IWorkspaceUser>(
            """
            select distinct
                   u.UserId
                  ,u.UserName
                  ,u.FirstName
                  ,u.LastName
                  ,IsWorkspaceAdmin = cast( case when CK.fAclGrantLevel( u.UserId, w.AclId ) >= 112 then 1 else 0 end as bit )
                  ,u.ExtendedCultureId
                  ,u.BinDate
              from CK.vUser u
                  inner join CK.tActorProfile ap on ap.ActorId = u.UserId
                  inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
              where ap.GroupId = @WorkspaceId and u.UserId > 1;
            """,
            new { WorkspaceId = workspaceId } );

        return users.Cast<IWorkspaceUser>().ToList();
    }
}
