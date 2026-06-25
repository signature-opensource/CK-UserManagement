using CK.Cris;

namespace CK.IO.UserManagement;

public interface IValidateInvitationTokenCommand : ICommand<IValidateInvitationTokenResult>
{
    public string Token { get; set; }
}
