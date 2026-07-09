using CK.Core;

namespace CK.IO.UserManagement;

public interface IPendingUser : IPoco
{
    public int DefaultXLCID { get; set; }
    public string Email { get; set; }
}
