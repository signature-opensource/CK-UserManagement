using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Forces the reset of a user's basic-authentication password: the administrator provides the new
/// <see cref="Password"/>, which is posed as a temporary one. The user can sign in with it right away
/// but must choose a new password (the temporary state is exposed on its profile as
/// <c>IsTemporaryPassword</c> and is cleared as soon as the user sets its own password).
/// </summary>
public interface IForceResetUserPasswordCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    /// <summary>The user whose password must be reset.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// The new password, posed as a temporary one. Required.
    /// </summary>
    public string Password { get; set; }
}
