using CK.Core;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Command part for a workspace-admin command that acts on pending invitations. The validator of this part
/// refuses the command when an invitation has no group in the current workspace, unless the actor is a
/// platform administrator.
/// </summary>
[CKTypeDefiner]
public interface ICommandWorkspaceInvitations : ICommandWorkspaceAdmin
{
    /// <summary>
    /// Gets or sets the invitations. Each invitation is identified by its <see cref="IPendingInvitation.Email"/>.
    /// </summary>
    List<IPendingInvitation> Invitations { get; set; }
}
