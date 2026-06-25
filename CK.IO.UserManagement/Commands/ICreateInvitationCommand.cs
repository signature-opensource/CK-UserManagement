using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface ICreateInvitationCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    public string Email { get; set; }
    public List<int> Groups { get; set; }
    public string CultureName { get; set; }
}
