using CK.AppIdentity;
using CK.AppIdentity.AuthCenter;
using CK.Core;
using Microsoft.Extensions.Configuration;

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
/// An invalid configuration throws.
/// </summary>
[PartyFeatureOption( "UserInvitation", TypeActivation.Required )]
public sealed class UserInvitationOption : IPartyComponentOption<LocalAuthCenterFeatureDriver>
{
    static readonly string[] _keys = ["Sender"];

    /// <summary>
    /// Initializes a new <see cref="UserInvitationOption"/> from its configuration.
    /// </summary>
    /// <param name="configuration">The "UserInvitation" section of the AuthCenter. Null for the defaults.</param>
    public UserInvitationOption( IConfiguration? configuration )
    {
        if( configuration != null )
        {
            foreach( var c in configuration.GetChildren() )
            {
                if( !_keys.Contains( c.Key ) ) Throw.CKException( $"Unknown \"{c.Key}\": the keys are {_keys.Concatenate()}." );
            }
            var sender = configuration["Sender"];
            Sender = string.IsNullOrEmpty( sender ) ? null : sender;
        }
    }

    /// <summary>
    /// Gets the name of the sender of the invitation mails (a sender of the "MailSender" feature of the AuthCenter party,
    /// CK.Mailer). Null: its default sender. An unknown sender fails the sending of a mail.
    /// </summary>
    public string? Sender { get; }
}
