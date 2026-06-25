using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetPlatformPendingInvitationsQCommand : ICommand<List<IPendingInvitation>>, ICommandCurrentCulture, ICommandPlatformAdmin
{
}
