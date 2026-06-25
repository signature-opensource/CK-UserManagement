using CK.Core;

namespace CK.IO.UserManagement;

public interface IPlatformUser : IPoco
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public DateTime? BinDate { get; set; }
}
