using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Destroys the given pending invitations: the invitation records are completely deleted. Mirrors
/// <see cref="IResendInvitationsCommand"/>: it carries the current workspace (guarded by
/// <c>AdminCommandValidator</c>) and operates by invitation e-mail.
/// </summary>
public interface IDestroyInvitationsCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    public List<IPendingInvitation> Invitations { get; set; }
}
