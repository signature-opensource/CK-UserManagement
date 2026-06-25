using CK.IO.Admin;
using CK.IO.User.BinnedUser;

namespace CK.IO.UserManagement;

/// <summary>
/// Workspace-scoped specialization of the package <see cref="IRestoreUsersCommand"/>.
/// <para>
/// It mixes in <see cref="ICommandWorkspaceAdmin"/> so the command carries the current workspace
/// (<c>CurrentWorkspaceId</c>) and is guarded by <c>AdminCommandValidator</c>. Being the closure
/// interface of the command family, the local handler that takes this type supersedes the package
/// handler (which only sees the base <see cref="IRestoreUsersCommand"/>) and forwards the workspace
/// to <c>CK.sUserRestore</c>.
/// </para>
/// </summary>
public interface IRestoreUsersAdminCommand : IRestoreUsersCommand, ICommandWorkspaceAdmin
{
}
