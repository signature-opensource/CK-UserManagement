using CK.Core;
using CK.IO.UserProfile.Workspace;

namespace CK.IO.UserManagement;

public interface IEditWorkspaceUserData : IPoco
{
    public IList<IGroupInfos> UserGroups { get; }
    public IList<IGroupInfos> WorkspaceGroups { get; }
}
