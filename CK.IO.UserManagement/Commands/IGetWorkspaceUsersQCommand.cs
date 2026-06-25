using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetWorkspaceUsersQCommand : ICommand<List<IWorkspaceUser>>, ICommandWorkspaceAdmin
{
}
