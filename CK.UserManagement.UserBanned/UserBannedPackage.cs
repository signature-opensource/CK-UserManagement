using CK.Core;
using CK.SqlServer;

namespace CK.UserManagement.UserBanned;

/// <summary>
/// Hosts the workspace-scoped banishment stored-procedure wrappers and owns the
/// <c>sUserBannedSet.tql</c> / <c>sUserBannedDestroy.tql</c> transformers (Res folder). The wrappers live
/// on the package rather than on a <c>[SqlTable("tUserBanned")]</c> specialization to keep this package
/// independent of the upstream <c>UserBannedTable</c>.
/// </summary>
[SqlPackage( Schema = "CK", ResourcePath = "Res" )]
[Versions( "1.0.0" )]
public abstract class UserBannedPackage : SqlPackage
{
    /// <summary>
    /// The reason key of the banishments set from the workspace administration. Bans carrying another
    /// key are set by the infrastructure (for instance <c>UserPassword.TooManyAttempt</c>).
    /// </summary>
    public const string AdminKeyReason = "UserManagement.AdminBan";

    // Depends on UserBanned (owns the base sUserBannedSet/sUserBannedDestroy transformed here) and on
    // the UserTable of CK.UserManagement (owns the CK.fUserAdministrationCheck scalar function that the
    // transformers call).
    void StObjConstruct( CK.DB.User.UserBanned.Package userBannedPackage,
                         CK.UserManagement.UserTable userTable )
    { }

    /// <summary>
    /// Creates or updates the banishment of a user for <paramref name="keyReason"/>. When
    /// <paramref name="workspaceId"/> is not 0, <c>sUserBannedSet.tql</c> applies <c>CK.fUserAdministrationCheck</c>:
    /// the caller must be able to administer the user from that workspace. When it is 0, the default platform
    /// route applies (System group).
    /// </summary>
    /// <param name="banStartDate">When null, utc now for a new banishment, unchanged for an existing one.</param>
    /// <param name="banEndDate">When null, the banishment is eternal (9999-12-31).</param>
    [SqlProcedure( "transform:sUserBannedSet" )]
    public abstract Task SetUserBannedAsync( ISqlCallContext ctx, int actorId, string keyReason, int userId,
                                             DateTime? banStartDate, DateTime? banEndDate, int workspaceId );

    /// <summary>
    /// Destroys the banishment of a user for <paramref name="keyReason"/>. When <paramref name="workspaceId"/>
    /// is not 0, <c>sUserBannedDestroy.tql</c> applies <c>CK.fUserAdministrationCheck</c>: the caller must be
    /// able to administer the user from that workspace. When it is 0, the default platform route applies
    /// (System group).
    /// </summary>
    [SqlProcedure( "transform:sUserBannedDestroy" )]
    public abstract Task DestroyUserBannedAsync( ISqlCallContext ctx, int actorId, string keyReason,
                                                 int userId, int workspaceId );
}
