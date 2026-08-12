using CK.Core;

namespace CK.IO.UserManagement.UserBanned;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IPlatformUser"/> with the banishments of the user
/// (<c>CK.tUserBanned</c>). See <see cref="IWorkspaceUser.Bans"/> for the exposed semantics.
/// </summary>
public interface IPlatformUser : CK.IO.UserManagement.IPlatformUser
{
    public IList<IUserBan> Bans { get; }
}
