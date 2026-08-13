using CK.Core;
using CK.IO.UserProfile.Workspace;

namespace CK.IO.UserManagement;

public interface IWorkspaceUser : IPoco
{
    public int UserId { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsWorkspaceAdmin { get; set; }
    public int ExtendedCultureId { get; set; }

    /// <summary>
    /// All the groups this user belongs to, across every workspace (zone), the zone groups themselves
    /// included: a user is a plain member of a workspace it belongs to without any other group.
    /// Each listing projection joins them to its own query.
    /// </summary>
    public IList<IGroupInfos> Groups { get; }
}
