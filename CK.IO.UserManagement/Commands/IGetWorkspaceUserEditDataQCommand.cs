using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IGetWorkspaceUserEditDataQCommand : ICommand<IEditWorkspaceUserData>, ICommandCurrentCulture, ICommandWorkspaceUserTarget
{
    public int UserId { get; set; }
}
