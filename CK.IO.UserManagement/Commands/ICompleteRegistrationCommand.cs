using CK.Core;
using CK.Cris;

namespace CK.IO.UserManagement;

public interface ICompleteRegistrationCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture
{
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int ExtendedCultureId { get; set; }
    public string Password { get; set; }
    public string Token { get; set; }

    /// <summary>
    /// Optional user name (nickname). When not provided, the e-mail is used as the user name.
    /// The e-mail always remains the identity check point.
    /// </summary>
    public string? UserName { get; set; }
}
