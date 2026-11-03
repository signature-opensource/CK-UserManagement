using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface ICreateInvitationCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceGroups
{
    public string Email { get; set; }
    public List<int> Groups { get; set; }
    public int ExtendedCultureId { get; set; }
}
