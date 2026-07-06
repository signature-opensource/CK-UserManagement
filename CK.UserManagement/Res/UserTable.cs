using CK.Core;
using CK.SqlServer;

namespace CK.UserManagement;

[SqlTable( "tUser", ResourcePath = "Res", Package = typeof( Package ) )]
[Versions( "1.0.0" )]
public abstract class UserTable : DB.Actor.UserTable
{
    // Depends on BinnedUser to order the setup: the workspace-user read model (IWorkspaceUser.BinDate,
    // read by UserManagementQueries.GetWorkspaceUsersAsync from CK.vUser) needs BinnedUser's vUser
    // transform to have added the BinDate column. The archive/restore write feature itself lives in
    // CK.UserManagement.BinnedUser.
    void StObjConstruct( DB.Acl.Package aclPackage,
                         CK.DB.User.BinnedUser.Package binnedUserPackage )
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
