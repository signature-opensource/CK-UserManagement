using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetWorkspaceUserEditDataQCommand : ICommand<IEditWorkspaceUserData>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    public int UserId { get; set; }
}
