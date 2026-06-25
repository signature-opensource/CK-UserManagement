using CK.Auth;
using CK.Core;
using CK.IO.Workspace;

namespace CK.IO.Admin;

/// <summary>
/// Marker for commands that require workspace-admin authority. The
/// admin validator accepts workspace administrators AND platform
/// administrators (platform admins implicitly administer every
/// workspace).
/// </summary>
[CKTypeDefiner]
public interface ICommandWorkspaceAdmin : ICommandWorkspace, ICommandAuthNormal
{
}
