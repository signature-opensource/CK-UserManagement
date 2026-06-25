using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetPlatformUsersQCommand : ICommand<List<IPlatformUser>>, ICommandCurrentCulture, ICommandPlatformAdmin
{
}
