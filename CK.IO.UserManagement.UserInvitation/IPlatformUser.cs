using CK.Core;

namespace CK.IO.UserManagement.UserInvitation;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IPlatformUser"/> with the user's primary e-mail.
/// </summary>
public interface IPlatformUser : CK.IO.UserManagement.IPlatformUser
{
    public string Email { get; set; }
}
