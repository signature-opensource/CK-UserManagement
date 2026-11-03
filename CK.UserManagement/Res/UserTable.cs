using CK.Core;
using CK.SqlServer;

namespace CK.UserManagement;

[SqlTable( "tUser", ResourcePath = "Res", Package = typeof( Package ) )]
[Versions( "1.0.0" )]
public abstract class UserTable : DB.Actor.UserTable
{
    // Acl orders the setup for fIsUserPlatformAdmin (uses CK.fAclGrantLevel). Workspace orders the setup
    // for fUserAdministrationCheck (reads CK.tWorkspace). The core is e-mail/ban-agnostic: no dependency on
    // ActorEMail or UserBanned here.
    void StObjConstruct( DB.Acl.Package aclPackage, DB.Workspace.WorkspaceTable workspaceTable )
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
    /// Checks if an actor can administer a user from a workspace. The actor can do it when the actor is a
    /// platform administrator. Otherwise, the user must be a member of the workspace, must not be a platform
    /// administrator or a System group member, and the actor must have SafeAdministrator (112) or more on
    /// every workspace of the user.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <param name="workspaceId">The workspace from which the actor acts.</param>
    /// <param name="userId">The user to administer.</param>
    /// <returns>A <see cref="UserAdministrationCheck"/> value: 0 (<see cref="UserAdministrationCheck.Allowed"/>) or the reason of the refusal.</returns>
    [SqlScalarFunction( "fUserAdministrationCheck" )]
    public abstract Task<byte> CheckUserAdministrationAsync( ISqlCallContext ctx, int actorId, int workspaceId, int userId );
}
