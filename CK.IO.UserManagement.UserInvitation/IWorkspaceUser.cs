using CK.Core;

namespace CK.IO.UserManagement.UserInvitation;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IWorkspaceUser"/> with the user's primary e-mail.
/// The Poco engine merges this into the single concrete workspace-user Poco when this package is present.
/// </summary>
public interface IWorkspaceUser : CK.IO.UserManagement.IWorkspaceUser
{
    public string Email { get; set; }
}
