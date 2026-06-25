using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetUserCreationDataQCommand : ICommand<IUserCreationDataResult>, ICommandPlatformAdmin
{
}
