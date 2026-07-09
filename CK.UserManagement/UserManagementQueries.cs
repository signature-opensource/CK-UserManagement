using CK.Core;
using CK.IO.UserManagement;
using CK.IO.UserProfile.Workspace;
using CK.SqlServer;
using Dapper;

namespace CK.UserManagement;

/// <summary>
/// Dapper read queries for the workspace-user management handlers, written against the standard CK.DB
/// schema (<c>CK.vUser</c>, <c>CK.tActorProfile</c>, <c>CK.vGroup</c>, <c>CK.vZone</c>). The core is
/// e-mail-agnostic (UserName only). Rows are materialized directly into their IPoco by Dapper
/// (CK.SqlServer.Dapper abstract type map). Invitation-specific reads (incl. e-mail) live in
/// <c>CK.UserManagement.UserInvitation.UserInvitationQueries</c>.
/// </summary>
public class UserManagementQueries : IAutoService
{
    readonly UserTable _userTable;

    public UserManagementQueries( UserTable userTable )
    {
        _userTable = userTable;
    }

    public async Task<IReadOnlyList<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx, int workspaceId )
    {
        var users = await ctx[_userTable].QueryAsync<IWorkspaceUser>(
            """
            select distinct
                   u.UserId
                  ,u.UserName
                  ,u.FirstName
                  ,u.LastName
                  ,IsWorkspaceAdmin = cast( case when CK.fAclGrantLevel( u.UserId, w.AclId ) >= 112 then 1 else 0 end as bit )
                  ,u.ExtendedCultureId
              from CK.vUser u
                  inner join CK.tActorProfile ap on ap.ActorId = u.UserId
                  inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
              where ap.GroupId = @WorkspaceId and u.UserId > 1;
            """,
            new { WorkspaceId = workspaceId } );

        return users.ToList();
    }

    public async Task<IReadOnlyList<IGroupInfos>> GetWorkspaceGroupsAsync( ISqlCallContext ctx, int workspaceId )
    {
        var groups = await ctx[_userTable].QueryAsync<IGroupInfos>(
            """
            select g.GroupId
                  ,g.GroupName
                  ,g.IsZone
                  ,g.ZoneId
                  ,ZoneName = isnull( z.ZoneName, '' )
              from CK.vGroup g
                  left outer join CK.vZone z on z.ZoneId = g.ZoneId
              where g.ZoneId = @WorkspaceId and g.GroupId > 1 and g.GroupName not like '%Operators';
            """,
            new { WorkspaceId = workspaceId } );

        return groups.ToList();
    }

    public async Task<IReadOnlyList<IGroupInfos>> GetUserWorkspaceGroupsAsync( ISqlCallContext ctx, int workspaceId, int userId )
    {
        var groups = await ctx[_userTable].QueryAsync<IGroupInfos>(
            """
            select g.GroupId
                  ,g.GroupName
                  ,g.IsZone
                  ,g.ZoneId
                  ,ZoneName = isnull( z.ZoneName, '' )
              from CK.vGroup g
                  inner join CK.tActorProfile ap on ap.GroupId = g.GroupId
                  left outer join CK.vZone z on z.ZoneId = g.ZoneId
              where ap.ActorId = @UserId and g.ZoneId = @WorkspaceId;
            """,
            new { WorkspaceId = workspaceId, UserId = userId } );

        return groups.ToList();
    }

    /// <summary>
    /// Ids of the groups (within the workspace zone) the user currently belongs to.
    /// Used to compute the add/remove delta when editing a workspace user.
    /// </summary>
    public async Task<IReadOnlyList<int>> GetUserWorkspaceGroupIdsAsync( ISqlCallContext ctx, int workspaceId, int userId )
    {
        var ids = await ctx[_userTable].QueryAsync<int>(
            """
            select ap.GroupId
              from CK.tActorProfile ap
                  inner join CK.vGroup g on g.GroupId = ap.GroupId
              where ap.ActorId = @UserId and g.ZoneId = @WorkspaceId and ap.GroupId <> ap.ActorId;
            """,
            new { WorkspaceId = workspaceId, UserId = userId } );
        return ids.ToList();
    }
}
