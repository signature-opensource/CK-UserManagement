namespace CK.UserManagement;

/// <summary>
/// The result of <see cref="UserTable.CheckUserAdministrationAsync"/> (the <c>CK.fUserAdministrationCheck</c> function).
/// </summary>
public enum UserAdministrationCheck : byte
{
    /// <summary>The actor can administer the user from the workspace.</summary>
    Allowed = 0,

    /// <summary>The actor or the workspace is 0 or less, or the user is the System user or less.</summary>
    InvalidParameters = 1,

    /// <summary>The user is not a member of the workspace.</summary>
    NotAWorkspaceMember = 2,

    /// <summary>The user is a platform administrator or a member of the System group.</summary>
    ProtectedUser = 3,

    /// <summary>The user is a member of a workspace where the actor is below SafeAdministrator (112).</summary>
    UnadministeredWorkspace = 4
}
