using CK.Core;

namespace CK.IO.UserManagement.UserBanned;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IWorkspaceUser"/> with the banishments of the user
/// (<c>CK.tUserBanned</c>). The Poco engine merges this into the single concrete workspace-user Poco when
/// this package is present.
/// <para>
/// All the banishments are exposed, expired ones included: the caller decides what "currently banned"
/// means by comparing <see cref="IUserBan.BanStartDate"/> and <see cref="IUserBan.BanEndDate"/> to the
/// current date. An empty list denotes a user that has never been banned.
/// </para>
/// </summary>
public interface IWorkspaceUser : CK.IO.UserManagement.IWorkspaceUser
{
    public IList<IUserBan> Bans { get; }
}
