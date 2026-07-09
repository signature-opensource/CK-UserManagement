using CK.Core;

namespace CK.IO.UserManagement.UserInvitation;

/// <summary>
/// Extends the core <see cref="CK.IO.UserManagement.IEditWorkspaceUserCommand"/> with the primary e-mail.
/// Updated as the primary <c>CK.tActorEMail</c> when it changes (handled by UserInvitation).
/// </summary>
public interface IEditWorkspaceUserCommand : CK.IO.UserManagement.IEditWorkspaceUserCommand
{
    public string Email { get; set; }
}
