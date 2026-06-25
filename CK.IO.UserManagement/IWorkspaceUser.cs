using CK.Core;

namespace CK.IO.UserManagement;

public interface IWorkspaceUser : IPoco
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsWorkspaceAdmin { get; set; }
    public int ExtendedCultureId { get; set; }
    public DateTime? BinDate { get; set; }
}
