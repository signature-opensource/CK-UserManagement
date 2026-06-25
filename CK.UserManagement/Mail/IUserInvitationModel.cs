using CK.Core;
using CK.Template.Fluid;

namespace CK.UserManagement.Mail;

/// <summary>
/// Data model for the user-invitation e-mail, bound to
/// <c>Res/Templates/UserInvitation.{Subject|Body}.{culture}.liquid</c>.
/// </summary>
[FluidTemplate( "UserInvitation" )]
public interface IUserInvitationModel : IPoco
{
    /// <summary>Root URL of the front-end (no trailing slash), e.g. <c>http://localhost:4200</c>.</summary>
    string FrontUrl { get; set; }

    /// <summary>One-time invitation token appended to the registration URL.</summary>
    string Token { get; set; }
}
