using CK.Core;
using CK.SqlServer;

namespace CK.UserManagement.BinnedUser;

/// <summary>
/// Hosts the workspace-scoped archive/restore stored-procedure wrappers and owns the
/// <c>sUserArchive.tql</c> / <c>sUserRestore.tql</c> transformers (Res folder). The wrappers live on
/// the package rather than on a <c>[SqlTable("tUser")]</c> specialization to keep this package
/// independent of the core <c>UserTable</c> and to avoid a second concrete <c>tUser</c> table.
/// </summary>
[SqlPackage( Schema = "CK", ResourcePath = "Res" )]
[Versions( "1.0.0" )]
public abstract class BinnedUserPackage : SqlPackage
{
    // Depends on BinnedUser (owns the base sUserArchive/sUserRestore transformed here) and on
    // Workspace (owns the CK.fUserWorkspaceGrantLevel scalar function the transforms call).
    void StObjConstruct( CK.DB.User.BinnedUser.Package binnedUserPackage,
                         DB.Workspace.WorkspaceTable workspaceTable )
    { }

    /// <summary>
    /// Archives a user if it exists, applying the workspace grant-level check injected by
    /// <c>sUserArchive.tql</c>: when <paramref name="workspaceId"/> is non-zero, the caller must be
    /// at least SafeAdministrator (112) on that workspace; when 0 the default platform route applies.
    /// </summary>
    [SqlProcedure( "transform:sUserArchive" )]
    public abstract Task ArchiveUserAsync( ISqlCallContext ctx, int actorId, int userId, int workspaceId );

    /// <summary>
    /// Restores an archived user if it exists, applying the workspace grant-level check injected by
    /// <c>sUserRestore.tql</c>: when <paramref name="workspaceId"/> is non-zero, the caller must be
    /// at least SafeAdministrator (112) on that workspace; when 0 the default platform route applies.
    /// </summary>
    [SqlProcedure( "transform:sUserRestore" )]
    public abstract Task RestoreUserAsync( ISqlCallContext ctx, int actorId, int userId, int workspaceId );
}
