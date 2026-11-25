using CK.AppIdentity;
using CK.AppIdentity.AuthCenter;
using CK.Core;
using CK.Mailer;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// The "UserInvitation" option of the AuthCenter: the invitations of the users of an Authentication Center. Always enabled
/// (the invitations have no off switch): its section configures it.
/// <code>
/// "AuthCenter": {
///   "UserInvitation": {
///     "Sender": "Support" // Optional: the sender of the mails ("MailSender" of the party), its default sender when absent.
///   }
/// }
/// </code>
/// Its configuration is <see cref="UserInvitationOptionConfiguration"/>: an invalid one is an error.
/// <para>
/// It requires the "MailSender" feature of the party (the invitation mails): the party can't turn it off.
/// </para>
/// </summary>
[PartyFeatureOption( "UserInvitation", TypeActivation.Required )]
[Configuration<UserInvitationOptionConfiguration>]
[Requires<MailSenderFeatureDriver>]
public sealed class UserInvitationOption : IPartyComponentOption<LocalAuthCenterFeatureDriver>
{
    /// <summary>
    /// Initializes a new <see cref="UserInvitationOption"/> from its configuration.
    /// </summary>
    /// <param name="configuration">The configuration of the option.</param>
    public UserInvitationOption( UserInvitationOptionConfiguration configuration )
    {
        Sender = string.IsNullOrEmpty( configuration.Sender ) ? null : configuration.Sender;
    }

    /// <summary>
    /// Gets the name of the sender of the invitation mails (a sender of the "MailSender" feature of the AuthCenter party,
    /// CK.Mailer). Null: its default sender. An unknown sender fails the sending of a mail.
    /// </summary>
    public string? Sender { get; }
}

/// <summary>
/// The configuration of the <see cref="UserInvitationOption"/>.
/// </summary>
[ConfigurationModel]
public sealed class UserInvitationOptionConfiguration
{
    /// <summary>
    /// Gets the name of the sender of the invitation mails ("MailSender" of the party), its default sender when null.
    /// </summary>
    public string? Sender { get; init; }
}
