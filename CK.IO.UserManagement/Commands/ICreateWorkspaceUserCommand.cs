using CK.Core;
using CK.Cris;
using CK.IO.Admin;

namespace CK.IO.UserManagement;

/// <summary>
/// Creates a workspace user directly (no invitation e-mail): the user is created immediately from its
/// <see cref="UserName"/> and added to the given <see cref="Groups"/> of the current workspace. The core
/// is e-mail-agnostic; sign-in credentials are provisioned as a basic <see cref="Password"/> so the user
/// can log in right away with its <see cref="UserName"/>.
/// </summary>
public interface ICreateWorkspaceUserCommand : ICommand<SimpleUserMessage>, ICommandCurrentCulture, ICommandWorkspaceAdmin
{
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int ExtendedCultureId { get; set; }

    /// <summary>
    /// Initial basic-authentication password set for the new user so it can sign in with its
    /// <see cref="UserName"/>. Required.
    /// </summary>
    public string Password { get; set; }

    /// <summary>Ids of the workspace groups the new user is added to.</summary>
    public List<int> Groups { get; set; }
}
