using CK.Core;
using CK.SqlServer;

namespace CK.UserManagement;

[SqlTable( "tUser", ResourcePath = "Res", Package = typeof( Package ) )]
[Versions( "1.0.0" )]
public abstract class UserTable : DB.Actor.UserTable
{
    // Depends on BinnedUser (owns sUserArchive/sUserRestore) and on Workspace (owns the
    // CK.fUserWorkspaceGrantLevel scalar function the transforms call) to order the setup.
    void StObjConstruct( DB.Acl.Package aclPackage,
                         CK.DB.User.BinnedUser.Package binnedUserPackage,
                         DB.Workspace.WorkspaceTable workspaceTable )
    { }


    /// <summary>
    /// Defines whether a user is a platform administrator.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <returns>True if user is a platform admin, false otherwise.</returns>
    [SqlScalarFunction( "fIsUserPlatformAdmin" )]
    public abstract Task<bool> IsUserPlatformAdminAsync( ISqlCallContext ctx, int actorId );

    /// <summary>
    /// Archives a user if it exists, applying the workspace grant-level check injected by
    /// <c>sUserArchive.tql</c>: when <paramref name="workspaceId"/> is non-zero, the caller must be
    /// at least SafeAdministrator (112) on that workspace; when 0 the default platform route applies.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="workspaceId">The workspace the action is scoped to, or 0 for the default route.</param>
    /// <returns>An awaitable.</returns>
    [SqlProcedure( "transform:sUserArchive" )]
    public abstract Task ArchiveUserAsync( ISqlCallContext ctx, int actorId, int userId, int workspaceId );

    /// <summary>
    /// Restores an archived user if it exists, applying the workspace grant-level check injected by
    /// <c>sUserRestore.tql</c>: when <paramref name="workspaceId"/> is non-zero, the caller must be
    /// at least SafeAdministrator (112) on that workspace; when 0 the default platform route applies.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="workspaceId">The workspace the action is scoped to, or 0 for the default route.</param>
    /// <returns>An awaitable.</returns>
    [SqlProcedure( "transform:sUserRestore" )]
    public abstract Task RestoreUserAsync( ISqlCallContext ctx, int actorId, int userId, int workspaceId );
}
