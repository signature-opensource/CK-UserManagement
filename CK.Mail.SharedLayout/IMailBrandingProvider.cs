using CK.Core;
using System.IO;

namespace CK.Mail.SharedLayout;

/// <summary>
/// Supplies the <see cref="IMailBranding"/> values applied to every outgoing
/// email rendered through <c>_DefaultMailLayout</c>, plus an optional inline
/// logo stream used to embed the header image as a <c>cid:</c> attachment
/// (bypasses client-side remote-image blocking in Outlook and friends).
/// </summary>
/// <remarks>
/// The default implementation reads branding from
/// <c>CK-AppIdentity:Local:MailBranding:*</c> and supplies no inline logo.
/// Tenant packages (e.g. <c>SLog.Mail.Branding</c>) override this provider
/// via <c>[ReplaceAutoService]</c> to ship a baked-in logo + brand defaults.
/// </remarks>
public interface IMailBrandingProvider : ISingletonAutoService
{
    /// <summary>
    /// Gets the current branding snapshot. The default implementation
    /// resolves once at construction from configuration.
    /// </summary>
    IMailBranding GetBranding();

    /// <summary>
    /// Opens a readable stream on the logo binary for CID inline embedding.
    /// Returns <c>null</c> when no inline logo is configured — the render path
    /// then falls back to <see cref="IMailBranding.LogoUrl"/>. The caller
    /// owns disposal of the returned stream.
    /// </summary>
    Stream? OpenLogo();

    /// <summary>
    /// Gets the Content-Id used for the inline logo attachment. Must match
    /// the <c>cid:</c> reference rendered into the layout. Empty string when
    /// <see cref="OpenLogo"/> returns <c>null</c>.
    /// </summary>
    string LogoContentId { get; }

    /// <summary>
    /// Gets the MIME content-type of the stream returned by <see cref="OpenLogo"/>
    /// (e.g. <c>image/png</c>). Empty string when no inline logo is configured.
    /// </summary>
    string LogoContentType { get; }
}
