using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetWorkspacePendingInvitationsQCommand : ICommand<List<IPendingInvitation>>, ICommandWorkspaceAdmin
{
}
