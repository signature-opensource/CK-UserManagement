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
    UnadministeredWorkspace = 4,

    /// <summary>The user is the actor: an actor manages its own account with the profile commands.</summary>
    SelfAdministration = 5,

    /// <summary>The user has a higher level than the actor on one of its workspaces.</summary>
    HigherLevelUser = 6,

    /// <summary>
    /// The action gives the account to the actor (its password, its primary e-mail) and the user has SafeAdministrator
    /// (112) or more on one of its workspaces: only a platform administrator can do it.
    /// </summary>
    ProtectedAccount = 7
}
