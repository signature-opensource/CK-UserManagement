using CK.Cris;

namespace CK.IO.UserManagement;

public interface IValidateInvitationTokenCommand : ICommand<IValidateInvitationTokenResult>, ICommandCurrentCulture
{
    public string Token { get; set; }
}
