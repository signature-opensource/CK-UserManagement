using CK.Core;

namespace CK.IO.UserManagement;
public interface IPendingInvitation : IPoco
{
    public string Email { get; set; }
    public bool Active { get; set; }
    public int ExtendedCultureId { get; set; }
    public string NativeName { get; set; }
    public DateTime ExpirationDateUtc { get; set; }
}
