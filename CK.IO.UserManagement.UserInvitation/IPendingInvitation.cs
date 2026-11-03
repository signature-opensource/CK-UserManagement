using CK.Core;

namespace CK.IO.UserManagement;
public interface IPendingInvitation : IPoco
{
    public string Email { get; set; }
    /// <summary>
    /// Gets or sets whether the registration link works now: the invitation is active and not expired.
    /// False for a deactivated or an expired invitation. A resend makes it true again.
    /// </summary>
    public bool Active { get; set; }
    public int ExtendedCultureId { get; set; }
    public string NativeName { get; set; }
    public DateTime ExpirationDateUtc { get; set; }
}
