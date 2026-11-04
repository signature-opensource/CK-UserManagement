using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IResendInvitationsCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceInvitations
{
    public List<IPendingInvitation> Invitations { get; set; }
}
