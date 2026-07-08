using CK.Core;
using CK.SqlServer;

namespace CK.UserManagement;

[SqlTable( "tUser", ResourcePath = "Res", Package = typeof( Package ) )]
[Versions( "1.0.0" )]
public abstract class UserTable : DB.Actor.UserTable
{
    // Acl orders the setup for fIsUserPlatformAdmin (uses CK.fAclGrantLevel). The core is
    // e-mail/archive-agnostic: no dependency on ActorEMail or BinnedUser here.
    void StObjConstruct( DB.Acl.Package aclPackage )
    { }


    /// <summary>
    /// Defines whether a user is a platform administrator.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <returns>True if user is a platform admin, false otherwise.</returns>
    [SqlScalarFunction( "fIsUserPlatformAdmin" )]
    public abstract Task<bool> IsUserPlatformAdminAsync( ISqlCallContext ctx, int actorId );
}
