using CK.Core;
using CK.IO.UserManagement;
using CK.UserManagement.Mail;

namespace CK.UserManagement.Tests;

/// <summary>
/// Test double that replaces <see cref="UserManagementMailer"/> so the user-management tests run
/// without the Fluid/branding/AppIdentity/SMTP infrastructure (the real mailer pulls in
/// <c>IDefaultEmailSender</c>, <c>IMailBrandingProvider</c>, <c>IFluidTemplateService</c> and
/// <c>IFrontUrlResolver</c>). It simply records the invitations it was asked to send so a test can
/// assert that an invitation e-mail was dispatched.
/// </summary>
[ReplaceAutoService( typeof( UserManagementMailer ) )]
public sealed class FakeUserManagementMailer : IUserManagementMailer
{
    public List<(string Destination, string Token, string CultureName)> Sent { get; } = new();

    public Task SendUserInvitationAsync( IActivityMonitor monitor, string destination, string token, string cultureName )
    {
        Sent.Add( (destination, token, cultureName) );
        monitor.Info( $"[FakeMailer] Invitation captured. (Email: {destination}, Culture: {cultureName})" );
        return Task.CompletedTask;
    }
}
