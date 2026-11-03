using CK.Core;
using CK.Template.Fluid;

namespace CK.UserManagement.UserInvitation.Mail;

/// <summary>
/// Data model for the user-invitation e-mail, bound to
/// <c>Res/Templates/UserInvitation.{Subject|Body}.{culture}.liquid</c>.
/// </summary>
[FluidTemplate( "UserInvitation" )]
public interface IUserInvitationModel : IPoco
{
    /// <summary>
    /// The URL (without trailing slash) of the default client application of the issuing party: <c>https://acme.com</c>.
    /// </summary>
    string ClientUrl { get; set; }

    /// <summary>
    /// The URL (without trailing slash) of each client application of the issuing party, by name: a template that targets
    /// another client application than the default one uses it (<c>{{ clientUrls.MiniApp }}</c>).
    /// </summary>
    IDictionary<string, string> ClientUrls { get; }

    /// <summary>One-time invitation token appended to the registration URL.</summary>
    string Token { get; set; }
}
