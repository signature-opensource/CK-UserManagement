using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Deactivates the given pending invitations: their registration link is invalidated (the invitation
/// is set inactive) so the invited person can no longer register, while the invitation record is kept
/// (it stays listed and can be resent). Mirrors <see cref="IResendInvitationsCommand"/>: it carries the
/// current workspace (guarded by <c>AdminCommandValidator</c>) and operates by invitation e-mail.
/// </summary>
public interface IDeactivateInvitationsCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    public List<IPendingInvitation> Invitations { get; set; }
}
