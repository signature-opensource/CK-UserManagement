using CK.Core;

namespace CK.IO.UserManagement;

public interface IValidateInvitationTokenResult : IPoco
{
    public SimpleUserMessage? UserMessage { get; set; }
    public IPendingUser? User { get; set; }
}
