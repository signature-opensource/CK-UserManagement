using CK.Core;
using CK.Cris;

namespace CK.IO.UserManagement;

public interface ICompleteRegistrationCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture
{
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string CultureName { get; set; }
    public string Password { get; set; }
    public string Token { get; set; }
}
