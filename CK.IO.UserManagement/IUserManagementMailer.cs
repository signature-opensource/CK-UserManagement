using CK.Core;

namespace CK.IO.UserManagement;

/// <summary>
/// Sends the user-management e-mails. The default implementation in <c>CK.UserManagement</c>
/// renders Fluid templates and dispatches through the CK-AppIdentity-configured mailer.
/// <para>
/// Two override points are available to consumers:
/// <list type="bullet">
///   <item>Override only the content: declare a <c>[FluidTemplatePackage]</c> and embed a
///   <c>Res/Templates/UserInvitation.{Subject|Body}.{culture}.liquid</c> with the same logical
///   name — the consumer registration wins over the default one.</item>
///   <item>Override the whole behavior: provide another <see cref="IUserManagementMailer"/> and
///   substitute it with <c>[ReplaceAutoService]</c>.</item>
/// </list>
/// </para>
/// </summary>
public interface IUserManagementMailer : IAutoService
{
    /// <summary>
    /// Sends a workspace invitation e-mail. The default templates are <c>UserInvitation.Subject</c>
    /// and <c>UserInvitation.Body</c>, bound to a model exposing <c>FrontUrl</c> and <c>Token</c>;
    /// the registration link is <c>{FrontUrl}/auth/register/{Token}</c>.
    /// </summary>
    /// <param name="monitor">The activity monitor.</param>
    /// <param name="destination">The recipient e-mail address.</param>
    /// <param name="token">The invitation token to embed in the registration link.</param>
    /// <param name="cultureName">The culture used to pick the template (falls back to French).</param>
    Task SendUserInvitationAsync( IActivityMonitor monitor, string destination, string token, string cultureName );
}
