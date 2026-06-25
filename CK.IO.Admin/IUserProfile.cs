namespace CK.IO.Admin;

/// <summary>
/// Admin-aspect extension of <see cref="Actor.IUserProfile"/>:
/// exposes whether the user has platform-admin authority.
/// </summary>
public interface IUserProfile : Actor.IUserProfile
{
    bool IsPlatformAdmin { get; set; }
}
