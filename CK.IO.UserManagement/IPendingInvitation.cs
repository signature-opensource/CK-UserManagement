using CK.Core;

namespace CK.IO.UserManagement;
public interface IPendingInvitation : IPoco
{
    public string Email { get; set; }
    public bool Active { get; set; }
    public string CultureName { get; set; }
    public string NativeName { get; set; }
    public DateTime ExpirationDateUtc { get; set; }
}
