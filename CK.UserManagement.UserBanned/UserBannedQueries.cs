using CK.Core;
using CK.IO.UserManagement;
using CK.IO.UserProfile.Workspace;
using CK.SqlServer;
using Dapper;

namespace CK.UserManagement.UserBanned;

/// <summary>
/// Dapper read queries for the UserBanned package. The ban-aware workspace-user listing reads the core
/// columns plus the banishments of each user (<c>CK.tUserBanned</c>) and materializes the UserBanned
/// extension Poco directly (CK.SqlServer.Dapper abstract type map).
/// </summary>
public class UserBannedQueries : IAutoService
{
    readonly UserBannedPackage _userBannedPackage;
    readonly PocoDirectory _pocoDirectory;

    /// <summary>
    /// The left outer joins duplicate the user row once per banishment and once per group — and, the two
    /// combined, once per (banishment, group) pair: the grouping is done in C# by
    /// <see cref="GetWorkspaceUsersWithBansAsync"/>. All the banishments are returned, expired ones
    /// included (hence CK.tUserBanned rather than CK.vUserCurrentlyBanned): the caller owns the
    /// definition of "currently banned".
    /// <para>
    /// The groups span all the zones, not only the queried workspace: the listing displays the
    /// memberships of a user outside of the current workspace. Rows are ordered by workspace, its own
    /// zone group first: a zone group is its own workspace but <c>CK.vGroup</c> gives it a null
    /// <c>ZoneId</c> (and therefore no <c>ZoneName</c>).
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
              ,b.KeyReason
              ,b.BanStartDate
              ,b.BanEndDate
              ,pg.GroupId
              ,pg.GroupName
              ,pg.IsZone
              ,pg.ZoneId
              ,ZoneName = isnull( pz.ZoneName, '' )
          from CK.vUser u
              inner join CK.tActorProfile ap on ap.ActorId = u.UserId
              inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
              left outer join CK.tUserBanned b on b.UserId = u.UserId
              left outer join CK.tActorProfile pap on pap.ActorId = u.UserId and pap.ActorId <> pap.GroupId
              left outer join CK.vGroup pg on pg.GroupId = pap.GroupId and pg.GroupId > 1
              left outer join CK.vZone pz on pz.ZoneId = pg.ZoneId
          where ap.GroupId = @WorkspaceId and u.UserId > 1
          order by u.UserId
                  ,case when pg.IsZone = 1 then pg.GroupId else pg.ZoneId end
                  ,pg.IsZone desc
                  ,pg.GroupName
                  ,b.BanStartDate;
        """;

    /// <summary>
    /// The ban part of a joined row. Members are nullable: a user without any banishment yields a row
    /// whose ban columns are all null (left outer join), that Dapper could not project onto the
    /// non-nullable <see cref="CK.IO.UserManagement.UserBanned.IUserBan"/> members. Dapper actually
    /// hands out a null BanRow altogether in that case.
    /// </summary>
    sealed class BanRow
    {
        public string? KeyReason { get; set; }
        public DateTime? BanStartDate { get; set; }
        public DateTime? BanEndDate { get; set; }
    }

    public UserBannedQueries( UserBannedPackage userBannedPackage, PocoDirectory pocoDirectory )
    {
        _userBannedPackage = userBannedPackage;
        _pocoDirectory = pocoDirectory;
    }

    public async Task<IReadOnlyList<IWorkspaceUser>> GetWorkspaceUsersWithBansAsync( ISqlCallContext ctx, int workspaceId )
    {
        var byId = new Dictionary<int, CK.IO.UserManagement.UserBanned.IWorkspaceUser>();
        await ctx[_userBannedPackage].QueryAsync<CK.IO.UserManagement.UserBanned.IWorkspaceUser, BanRow, IGroupInfos, object?>(
            _getWorkspaceUsersSql,
            ( user, ban, group ) =>
            {
                if( !byId.TryGetValue( user.UserId, out var existing ) )
                {
                    byId.Add( user.UserId, existing = user );
                }
                // Dapper hands out a null joined object when all its columns are null, which is what the
                // left outer joins produce for a user that has never been banned (or has no group).
                // Both collections are guarded: the banishment and group joins fan out into each other,
                // so each banishment row repeats once per group and each group row once per banishment.
                if( ban?.KeyReason != null
                    && !existing.Bans.Any( x => x.KeyReason == ban.KeyReason && x.BanStartDate == ban.BanStartDate ) )
                {
                    existing.Bans.Add( _pocoDirectory.Create<CK.IO.UserManagement.UserBanned.IUserBan>( b =>
                    {
                        b.KeyReason = ban.KeyReason;
                        b.BanStartDate = ban.BanStartDate!.Value;
                        b.BanEndDate = ban.BanEndDate!.Value;
                    } ) );
                }
                if( group != null && !existing.Groups.Any( g => g.GroupId == group.GroupId ) )
                {
                    existing.Groups.Add( group );
                }
                return null;
            },
            new { WorkspaceId = workspaceId },
            splitOn: "KeyReason,GroupId" );

        return byId.Values.Cast<IWorkspaceUser>().ToList();
    }
}
