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
    // Workspace (owns the CK.fUserWorkspaceGrantLevel scalar function the transforms call).
    void StObjConstruct( CK.DB.User.UserBanned.Package userBannedPackage,
                         DB.Workspace.WorkspaceTable workspaceTable )
    { }

    /// <summary>
    /// Creates or updates the banishment of a user for <paramref name="keyReason"/>, applying the
    /// workspace grant-level check injected by <c>sUserBannedSet.tql</c>: when
    /// <paramref name="workspaceId"/> is non-zero, the caller must be at least SafeAdministrator (112)
    /// on that workspace; when 0 the default platform route applies.
    /// </summary>
    /// <param name="banStartDate">When null, utc now for a new banishment, unchanged for an existing one.</param>
    /// <param name="banEndDate">When null, the banishment is eternal (9999-12-31).</param>
    [SqlProcedure( "transform:sUserBannedSet" )]
    public abstract Task SetUserBannedAsync( ISqlCallContext ctx, int actorId, string keyReason, int userId,
                                             DateTime? banStartDate, DateTime? banEndDate, int workspaceId );

    /// <summary>
    /// Destroys the banishment of a user for <paramref name="keyReason"/>, applying the workspace
    /// grant-level check injected by <c>sUserBannedDestroy.tql</c>: when <paramref name="workspaceId"/>
    /// is non-zero, the caller must be at least SafeAdministrator (112) on that workspace; when 0 the
    /// default platform route applies.
    /// </summary>
    [SqlProcedure( "transform:sUserBannedDestroy" )]
    public abstract Task DestroyUserBannedAsync( ISqlCallContext ctx, int actorId, string keyReason,
                                                 int userId, int workspaceId );
}
