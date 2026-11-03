using CK.Core;
using CK.Cris;

namespace CK.IO.UserManagement;

/// <summary>
/// Completes the registration of an invited user. The user's e-mail is the address the invitation was sent to: it is
/// not part of this command (it could only repeat it, or contradict it).
/// </summary>
public interface ICompleteRegistrationCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int ExtendedCultureId { get; set; }
    public string Password { get; set; }
    public string Token { get; set; }

    /// <summary>
    /// Optional user name (nickname). When not provided, the invitation's e-mail is used as the user name.
    /// The e-mail always remains the identity check point.
    /// </summary>
    public string? UserName { get; set; }
}
