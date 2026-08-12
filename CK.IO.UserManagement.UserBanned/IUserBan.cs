using CK.Core;

namespace CK.IO.UserManagement.UserBanned;

/// <summary>
/// A single banishment of a user: one row of <c>CK.tUserBanned</c>. A user can carry several
/// banishments at once, discriminated by <see cref="KeyReason"/> (the table primary key is
/// <c>(UserId, KeyReason)</c>).
/// </summary>
public interface IUserBan : IPoco
{
    /// <summary>
    /// Gets or sets the reason of the banishment. It is the discriminator of the banishment: bans set by
    /// the workspace administration use <c>UserManagement.AdminBan</c>, others are set by the
    /// infrastructure (for instance <c>UserPassword.TooManyAttempt</c>).
    /// </summary>
    string KeyReason { get; set; }

    /// <summary>
    /// Gets or sets the start date (UTC) of the banishment.
    /// </summary>
    DateTime BanStartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date (UTC) of the banishment. <c>9999-12-31</c> denotes an eternal banishment.
    /// </summary>
    DateTime BanEndDate { get; set; }
}
