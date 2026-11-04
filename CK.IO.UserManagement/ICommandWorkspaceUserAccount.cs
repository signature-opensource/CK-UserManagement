using CK.Core;

namespace CK.IO.UserManagement;

/// <summary>
/// Command part for a workspace-admin command that gives the account of <see cref="ICommandWorkspaceUserTarget.UserId"/>
/// to the actor: it sets the user's password (or its primary e-mail, through which the password can be recovered).
/// Besides the rule of <see cref="ICommandWorkspaceUserTarget"/>, such a command is refused for a user that has
/// SafeAdministrator (112) or more on one of its workspaces: only a platform administrator can do it.
/// </summary>
[CKTypeDefiner]
public interface ICommandWorkspaceUserAccount : ICommandWorkspaceUserTarget
{
}
