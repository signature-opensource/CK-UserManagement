using CK.Core;
using CK.IO.UserManagement;
using CK.UserManagement.UserInvitation.Mail;

namespace CK.UserManagement.UserInvitation.Tests;

/// <summary>
/// Test double that replaces <see cref="UserManagementMailer"/> so the user-management tests run
/// without the Fluid/branding/AppIdentity/SMTP infrastructure (the real mailer pulls in
/// <c>IDefaultEmailSender</c>, <c>IMailBrandingProvider</c>, <c>IFluidTemplateService</c> and
/// <c>CurrentLocalParty</c>). It simply records the invitations it was asked to send so a test can
/// assert that an invitation e-mail was dispatched.
/// </summary>
[ReplaceAutoService( typeof( UserManagementMailer ) )]
public sealed class FakeUserManagementMailer : IUserManagementMailer
{
    public List<(string Destination, string Token, int ExtendedCultureId, int IssuingPartyId)> Sent { get; } = new();

    /// <summary>
    /// Gets or sets whether the issuing party has a client application URL: false simulates a party that states none
    /// (no mail is sent).
    /// </summary>
    public bool HasClientUrl { get; set; } = true;

    public Task<bool> SendUserInvitationAsync( IActivityMonitor monitor, string destination, string token, int extendedCultureId, int issuingPartyId )
    {
        if( !HasClientUrl ) return Task.FromResult( false );
        Sent.Add( (destination, token, extendedCultureId, issuingPartyId) );
        monitor.Info( $"[FakeMailer] Invitation captured. (Email: {destination}, XLCID: {extendedCultureId}, IssuingPartyId: {issuingPartyId})" );
        return Task.FromResult( true );
    }
}
