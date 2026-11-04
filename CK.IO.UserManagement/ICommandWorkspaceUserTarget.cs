using CK.Core;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Command part for a workspace-admin command that acts on one user. The validator of this part refuses the
/// command when the actor cannot administer <see cref="UserId"/> from the current workspace. The actor can
/// do it when the actor is a platform administrator. Otherwise, the user must not be the actor (its own account is
/// managed from its profile), must be a member of the workspace, must not be a platform administrator or a System
/// group member, and on every workspace of the user the actor must have SafeAdministrator (112) or more and at least
/// the level of the user. See <see cref="ICommandWorkspaceUserAccount"/> for the commands that give the account to
/// the actor.
/// </summary>
[CKTypeDefiner]
public interface ICommandWorkspaceUserTarget : ICommandWorkspaceAdmin
{
    /// <summary>
    /// Gets or sets the user on which the command acts.
    /// </summary>
    int UserId { get; set; }
}
