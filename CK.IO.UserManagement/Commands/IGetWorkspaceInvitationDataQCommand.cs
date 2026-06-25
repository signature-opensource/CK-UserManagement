using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetWorkspaceInvitationDataQCommand : ICommand<IWorkspaceInvitationData>, ICommandWorkspaceAdmin
{
}
