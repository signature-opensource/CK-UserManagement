using CK.Core;

namespace CK.IO.UserManagement.BinnedUser;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IWorkspaceUser"/> with the archive date
/// (<c>BinDate</c>). The Poco engine merges this into the single concrete workspace-user Poco when this
/// package is present.
/// </summary>
public interface IWorkspaceUser : CK.IO.UserManagement.IWorkspaceUser
{
    public DateTime? BinDate { get; set; }
}
