using CK.Core;
using CK.IO.UserManagement;
using CK.IO.UserProfile.Workspace;
using CK.SqlServer;
using Dapper;

namespace CK.UserManagement;

/// <summary>
/// Dapper read queries for the workspace-user management handlers, written against the standard CK.DB
/// schema (<c>CK.vUser</c>, <c>CK.tActorProfile</c>, <c>CK.vGroup</c>, <c>CK.vZone</c>,
/// <c>CK.tActorEMail</c>). Results are projected to flat records and rebuilt as Pocos through the
/// <see cref="PocoDirectory"/>. Invitation-specific reads live in
/// <c>CK.UserManagement.UserInvitation.UserInvitationQueries</c>.
/// </summary>
public class UserManagementQueries : IAutoService
{
    readonly PocoDirectory _pocoDir;
    readonly UserTable _userTable;

    public UserManagementQueries( PocoDirectory pocoDirectory, UserTable userTable )
    {
        _pocoDir = pocoDirectory;
        _userTable = userTable;
    }

    public async Task<IReadOnlyList<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx, int workspaceId )
    {
        var rows = await ctx[_userTable].QueryAsync<FlatWorkspaceUser>(
            """
            select distinct
                   u.UserId
                  ,u.UserName
                  ,Email = isnull( e.EMail, '' )
                  ,u.FirstName
                  ,u.LastName
                  ,IsWorkspaceAdmin = cast( case when CK.fAclGrantLevel( u.UserId, w.AclId ) >= 112 then 1 else 0 end as bit )
                  ,u.ExtendedCultureId
                  ,u.BinDate
              from CK.vUser u
                  inner join CK.tActorProfile ap on ap.ActorId = u.UserId
                  inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
                  left outer join CK.tActorEMail e on e.ActorId = u.UserId and e.IsPrimary = 1
              where ap.GroupId = @WorkspaceId and u.UserId > 1;
            """,
            new { WorkspaceId = workspaceId } );

        return rows.Select( r => _pocoDir.Create<IWorkspaceUser>( u =>
        {
            u.UserId = r.UserId;
            u.UserName = r.UserName;
            u.Email = r.Email;
            u.FirstName = r.FirstName;
            u.LastName = r.LastName;
            u.IsWorkspaceAdmin = r.IsWorkspaceAdmin;
            u.ExtendedCultureId = r.ExtendedCultureId;
            u.BinDate = r.BinDate;
        } ) ).ToList();
    }

    public async Task<IReadOnlyList<IGroupInfos>> GetWorkspaceGroupsAsync( ISqlCallContext ctx, int workspaceId )
    {
        var rows = await ctx[_userTable].QueryAsync<FlatGroup>(
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

        return rows.Select( MapGroup ).ToList();
    }

    public async Task<IReadOnlyList<IGroupInfos>> GetUserWorkspaceGroupsAsync( ISqlCallContext ctx, int workspaceId, int userId )
    {
        var rows = await ctx[_userTable].QueryAsync<FlatGroup>(
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

        return rows.Select( MapGroup ).ToList();
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

    /// <summary>
    /// Reads the current primary e-mail of a user from <c>CK.tActorEMail</c>, or <c>null</c> when the
    /// user has no primary e-mail.
    /// </summary>
    public Task<string?> GetPrimaryEmailAsync( ISqlCallContext ctx, int userId )
    {
        return ctx[_userTable].QuerySingleOrDefaultAsync<string?>(
            "select EMail from CK.tActorEMail where ActorId = @UserId and IsPrimary = 1;",
            new { UserId = userId } );
    }

    IGroupInfos MapGroup( FlatGroup g ) => _pocoDir.Create<IGroupInfos>( gi =>
    {
        gi.GroupId = g.GroupId;
        gi.GroupName = g.GroupName;
        gi.IsZone = g.IsZone;
        gi.ZoneId = g.ZoneId;
        gi.ZoneName = g.ZoneName;
    } );

    record FlatWorkspaceUser
    {
        public int UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public bool IsWorkspaceAdmin { get; init; }
        public int ExtendedCultureId { get; init; }
        public DateTime? BinDate { get; set; } = null;
    }

    record FlatGroup
    {
        public int GroupId { get; init; }
        public string GroupName { get; init; } = string.Empty;
        public bool IsZone { get; init; }
        public int ZoneId { get; init; }
        public string ZoneName { get; init; } = string.Empty;
    }
}
