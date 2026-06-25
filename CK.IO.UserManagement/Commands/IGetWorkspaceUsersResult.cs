using CK.Core;

namespace CK.IO.UserManagement;

public interface IGetWorkspaceUsersResult : IPoco
{
    public List<IWorkspaceUser> Users { get; set; }
}
