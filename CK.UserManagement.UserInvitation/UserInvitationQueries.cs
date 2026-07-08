using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using CK.UserManagement;
using Dapper;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// Dapper read queries for the invitation handlers. Pending invitations are read directly from
/// <c>CK.tUserInvitation</c> (and its group satellite) so every invitation is visible regardless of
/// the administrator who created it; the workspace an invitation belongs to is derived from the zone
/// of its groups. Results are projected to flat records and rebuilt as Pocos through the
/// <see cref="PocoDirectory"/>. The core <see cref="UserTable"/> is used as the query anchor.
/// </summary>
public class UserInvitationQueries : IAutoService
{
    readonly PocoDirectory _pocoDir;
    readonly UserTable _userTable;

    public UserInvitationQueries( PocoDirectory pocoDirectory, UserTable userTable )
    {
        _pocoDir = pocoDirectory;
        _userTable = userTable;
    }

    /// <summary>
    /// E-mail-aware workspace-user listing: the core columns plus the primary e-mail
    /// (<c>CK.tActorEMail</c>, <c>IsPrimary = 1</c>). Builds the merged Poco through its UserInvitation
    /// extension so the <c>Email</c> property is set.
    /// </summary>
    public async Task<IReadOnlyList<IWorkspaceUser>> GetWorkspaceUsersWithEmailAsync( ISqlCallContext ctx, int workspaceId )
    {
        // Dapper materializes the UserInvitation extension Poco directly (abstract type map);
        // the Email column maps to its Email property.
        var users = await ctx[_userTable].QueryAsync<CK.IO.UserManagement.UserInvitation.IWorkspaceUser>(
            """
            select distinct
                   u.UserId
                  ,u.UserName
                  ,Email = isnull( e.EMail, '' )
                  ,u.FirstName
                  ,u.LastName
                  ,IsWorkspaceAdmin = cast( case when CK.fAclGrantLevel( u.UserId, w.AclId ) >= 112 then 1 else 0 end as bit )
                  ,u.ExtendedCultureId
              from CK.vUser u
                  inner join CK.tActorProfile ap on ap.ActorId = u.UserId
                  inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
                  left outer join CK.tActorEMail e on e.ActorId = u.UserId and e.IsPrimary = 1
              where ap.GroupId = @WorkspaceId and u.UserId > 1;
            """,
            new { WorkspaceId = workspaceId } );

        return users.Cast<IWorkspaceUser>().ToList();
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

    /// <summary>
    /// Pending (non-expired) invitations read directly from <c>CK.tUserInvitation</c>, regardless of
    /// the administrator who created them. When <paramref name="workspaceId"/> is provided, only
    /// invitations whose groups belong to that workspace zone are returned; otherwise every pending
    /// invitation is returned (platform-wide).
    /// </summary>
    public async Task<IReadOnlyList<IPendingInvitation>> GetPendingInvitationsAsync( ISqlCallContext ctx, int? workspaceId = null )
    {
        var rows = await ctx[_userTable].QueryAsync<FlatInvitation>(
            """
            select ui.InvitationId
                  ,ui.UserTargetAddress
                  ,ui.IsActive
                  ,ui.ExpirationDateUtc
                  ,ui.CultureId
                  ,uig.GroupId
              from CK.tUserInvitation ui
                  left outer join CK.tUserInvitationGroup uig on uig.InvitationId = ui.InvitationId
              where ui.InvitationId > 0 and ui.ExpirationDateUtc > @Now;
            """,
            new { Now = DateTime.UtcNow } );

        var pending = rows
            .GroupBy( r => r.InvitationId )
            .Select( g =>
            {
                var first = g.First();
                return new
                {
                    first.UserTargetAddress,
                    first.IsActive,
                    first.ExpirationDateUtc,
                    first.CultureId,
                    GroupIdentifiers = g.Where( r => r.GroupId.HasValue ).Select( r => r.GroupId!.Value ).ToList()
                };
            } )
            .ToList();

        if( workspaceId is > 0 )
        {
            var groupIds = pending.SelectMany( i => i.GroupIdentifiers ).Distinct().ToList();
            var groupWorkspaces = await GetGroupWorkspacesAsync( ctx, groupIds );
            pending = pending.Where( i => i.GroupIdentifiers.Any( g => groupWorkspaces.TryGetValue( g, out var w ) && w == workspaceId.Value ) ).ToList();
        }

        var cultures = await GetCultureNamesAsync( ctx, pending.Select( i => i.CultureId ).Distinct().ToList() );

        return pending.Select( i =>
        {
            cultures.TryGetValue( i.CultureId, out var culture );
            return _pocoDir.Create<IPendingInvitation>( p =>
            {
                p.Email = i.UserTargetAddress;
                p.Active = i.IsActive;
                p.ExtendedCultureId = i.CultureId;
                p.NativeName = culture?.NativeName ?? "Français";
                p.ExpirationDateUtc = i.ExpirationDateUtc;
            } );
        } ).ToList();
    }

    /// <summary>
    /// Reads a single invitation by its target e-mail (unique platform-wide), regardless of the
    /// administrator who created it. Returns <c>null</c> when no invitation exists for that e-mail.
    /// </summary>
    public Task<FlatInvitationByEmail?> GetInvitationByEmailAsync( ISqlCallContext ctx, string email )
    {
        return ctx[_userTable].QuerySingleOrDefaultAsync<FlatInvitationByEmail?>(
            """
            select top 1 InvitationId
                        ,CreatedById
                        ,IsActive
                        ,ExpirationDateUtc
                        ,CultureId
              from CK.tUserInvitation
              where UserTargetAddress = @Email and InvitationId > 0;
            """,
            new { Email = email } );
    }

    /// <summary>
    /// Reads the secret of an invitation by its id, regardless of the administrator who created it.
    /// </summary>
    public Task<byte[]?> GetInvitationSecretAsync( ISqlCallContext ctx, int invitationId )
    {
        return ctx[_userTable].QuerySingleOrDefaultAsync<byte[]?>(
            "select [Secret] from CK.tUserInvitation where InvitationId = @InvitationId;",
            new { InvitationId = invitationId } );
    }

    /// <summary>
    /// Derives the workspace an invitation targets from its groups.
    /// Returns the first resolved workspace, or 0 when none can be resolved.
    /// </summary>
    public async Task<int> GetWorkspaceIdForGroupsAsync( ISqlCallContext ctx, IEnumerable<int> groupIds )
    {
        var workspaces = await GetGroupWorkspacesAsync( ctx, groupIds );
        return workspaces.Values.FirstOrDefault( w => w > 0 );
    }

    /// <summary>
    /// Maps the given group ids to the workspace (zone) they belong to. A workspace is a zone, so a
    /// zone group carries the workspace on its own <c>GroupId</c> (its <c>ZoneId</c> is 0), while a
    /// regular group carries it on its <c>ZoneId</c>.
    /// </summary>
    async Task<Dictionary<int, int>> GetGroupWorkspacesAsync( ISqlCallContext ctx, IEnumerable<int> groupIds )
    {
        var ids = groupIds.ToList();
        if( ids.Count == 0 ) return new();
        var rows = await ctx[_userTable].QueryAsync<FlatGroupWorkspace>(
            "select GroupId, WorkspaceId = case when IsZone = 1 then GroupId else ZoneId end from CK.vGroup where GroupId in @Ids;",
            new { Ids = ids } );
        return rows.ToDictionary( r => r.GroupId, r => r.WorkspaceId );
    }

    /// <summary>Resolves the culture name and native name for the given culture ids.</summary>
    async Task<Dictionary<int, FlatCulture>> GetCultureNamesAsync( ISqlCallContext ctx, IEnumerable<int> cultureIds )
    {
        var ids = cultureIds.ToList();
        if( ids.Count == 0 ) return new();
        var rows = await ctx[_userTable].QueryAsync<FlatCulture>(
            "select CultureId, Name, NativeName from CK.tCulture where CultureId in @Ids;",
            new { Ids = ids } );
        return rows.ToDictionary( r => r.CultureId, r => r );
    }

    record FlatInvitation
    {
        public int InvitationId { get; init; }
        public string UserTargetAddress { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime ExpirationDateUtc { get; init; }
        public int CultureId { get; init; }
        public int? GroupId { get; init; }
    }

    public record FlatInvitationByEmail
    {
        public int InvitationId { get; init; }
        public int CreatedById { get; init; }
        public bool IsActive { get; init; }
        public DateTime ExpirationDateUtc { get; init; }
        public int CultureId { get; init; }
    }

    record FlatGroupWorkspace
    {
        public int GroupId { get; init; }
        public int WorkspaceId { get; init; }
    }

    record FlatCulture
    {
        public int CultureId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string NativeName { get; init; } = string.Empty;
    }

}
