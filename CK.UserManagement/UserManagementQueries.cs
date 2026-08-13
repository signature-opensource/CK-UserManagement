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

    /// <summary>
    /// The left outer joins on the groups return one row per group of the user, across all the zones
    /// (not only the queried workspace): the listing displays the memberships of a user outside of the
    /// current workspace. The zone groups themselves are kept (they carry the mere membership of a
    /// workspace); the actor's own profile row and the system group are excluded. The grouping is done
    /// in C# by <see cref="GetWorkspaceUsersAsync"/>.
    /// <para>
    /// Rows are ordered by workspace, its own zone group first: a zone group is its own workspace but
    /// <c>CK.vGroup</c> gives it a null <c>ZoneId</c> (and therefore no <c>ZoneName</c>).
    /// </para>
    /// </summary>
    const string _getWorkspaceUsersSql =
        """
        select u.UserId
              ,u.UserName
              ,u.FirstName
              ,u.LastName
              ,IsWorkspaceAdmin = cast( case when CK.fAclGrantLevel( u.UserId, w.AclId ) >= 112 then 1 else 0 end as bit )
              ,u.ExtendedCultureId
              ,pg.GroupId
              ,pg.GroupName
              ,pg.IsZone
              ,pg.ZoneId
              ,ZoneName = isnull( pz.ZoneName, '' )
          from CK.vUser u
              inner join CK.tActorProfile ap on ap.ActorId = u.UserId
              inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
              left outer join CK.tActorProfile pap on pap.ActorId = u.UserId and pap.ActorId <> pap.GroupId
              left outer join CK.vGroup pg on pg.GroupId = pap.GroupId and pg.GroupId > 1
              left outer join CK.vZone pz on pz.ZoneId = pg.ZoneId
          where ap.GroupId = @WorkspaceId and u.UserId > 1
          order by u.UserId
                  ,case when pg.IsZone = 1 then pg.GroupId else pg.ZoneId end
                  ,pg.IsZone desc
                  ,pg.GroupName;
        """;

    public UserManagementQueries( UserTable userTable )
    {
        _userTable = userTable;
    }

    public async Task<IReadOnlyList<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx, int workspaceId )
    {
        var byId = new Dictionary<int, IWorkspaceUser>();
        await ctx[_userTable].QueryAsync<IWorkspaceUser, IGroupInfos, object?>(
            _getWorkspaceUsersSql,
            ( user, group ) =>
            {
                if( !byId.TryGetValue( user.UserId, out var existing ) )
                {
                    byId.Add( user.UserId, existing = user );
                }
                // Dapper hands out a null second object when all its columns are null: a user without any
                // group (which the workspace membership makes impossible in practice).
                if( group != null && !existing.Groups.Any( g => g.GroupId == group.GroupId ) )
                {
                    existing.Groups.Add( group );
                }
                return null;
            },
            new { WorkspaceId = workspaceId },
            splitOn: "GroupId" );

        return byId.Values.ToList();
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
