using CK.Core;
using CK.IO.UserManagement;
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
    /// The left outer join duplicates the user row once per banishment: the grouping is done in C# by
    /// <see cref="GetWorkspaceUsersWithBansAsync"/>. All the banishments are returned, expired ones
    /// included (hence CK.tUserBanned rather than CK.vUserCurrentlyBanned): the caller owns the
    /// definition of "currently banned".
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
          from CK.vUser u
              inner join CK.tActorProfile ap on ap.ActorId = u.UserId
              inner join CK.tWorkspace w on w.WorkspaceId = @WorkspaceId
              left outer join CK.tUserBanned b on b.UserId = u.UserId
          where ap.GroupId = @WorkspaceId and u.UserId > 1
          order by u.UserId, b.BanStartDate;
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
        await ctx[_userBannedPackage].QueryAsync<CK.IO.UserManagement.UserBanned.IWorkspaceUser, BanRow, object?>(
            _getWorkspaceUsersSql,
            ( user, ban ) =>
            {
                if( !byId.TryGetValue( user.UserId, out var existing ) )
                {
                    byId.Add( user.UserId, existing = user );
                }
                // Dapper hands out a null second object when all its columns are null, which is what the
                // left outer join produces for a user that has never been banned.
                if( ban?.KeyReason != null )
                {
                    existing.Bans.Add( _pocoDirectory.Create<CK.IO.UserManagement.UserBanned.IUserBan>( b =>
                    {
                        b.KeyReason = ban.KeyReason;
                        b.BanStartDate = ban.BanStartDate!.Value;
                        b.BanEndDate = ban.BanEndDate!.Value;
                    } ) );
                }
                return null;
            },
            new { WorkspaceId = workspaceId },
            splitOn: "KeyReason" );

        return byId.Values.Cast<IWorkspaceUser>().ToList();
    }
}
