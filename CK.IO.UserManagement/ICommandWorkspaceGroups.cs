using CK.Core;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Command part for a workspace-admin command that gives groups to a user. The validator of this part
/// refuses the command when a group is not the current workspace or a group of the current workspace.
/// </summary>
[CKTypeDefiner]
public interface ICommandWorkspaceGroups : ICommandWorkspaceAdmin
{
    /// <summary>
    /// Gets or sets the identifiers of the groups.
    /// </summary>
    List<int> Groups { get; set; }
}
