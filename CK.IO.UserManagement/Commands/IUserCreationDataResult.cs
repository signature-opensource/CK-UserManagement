using CK.Core;
using CK.IO.Globalization;
using CK.IO.UserProfile.Workspace;

namespace CK.IO.UserManagement;

public interface IUserCreationDataResult : IPoco
{
    public IList<ICulture> Languages { get; }
    public IList<IGroupInfos> Groups { get; }
}
