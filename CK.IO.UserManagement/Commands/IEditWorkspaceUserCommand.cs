using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

public interface IEditWorkspaceUserCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    public int UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string UserName { get; set; }
    public int ExtendedCultureId { get; set; }
    public List<int> Groups { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// The user's primary e-mail. Updated as the primary <c>CK.tActorEMail</c> when it changes.
    /// </summary>
    public string Email { get; set; }
}
