using CK.Core;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Command part for a workspace-admin command that acts on one user. The validator of this part refuses the
/// command when the actor cannot administer <see cref="UserId"/> from the current workspace. The actor can
/// do it when the actor is a platform administrator. Otherwise, the user must be a member of the workspace,
/// must not be a platform administrator or a System group member, and the actor must have SafeAdministrator
/// (112) or more on every workspace of the user.
/// </summary>
[CKTypeDefiner]
public interface ICommandWorkspaceUserTarget : ICommandWorkspaceAdmin
{
    /// <summary>
    /// Gets or sets the user on which the command acts.
    /// </summary>
    int UserId { get; set; }
}
