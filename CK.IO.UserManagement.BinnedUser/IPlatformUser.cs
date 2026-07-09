using CK.Core;

namespace CK.IO.UserManagement.BinnedUser;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IPlatformUser"/> with the archive date (<c>BinDate</c>).
/// </summary>
public interface IPlatformUser : CK.IO.UserManagement.IPlatformUser
{
    public DateTime? BinDate { get; set; }
}
