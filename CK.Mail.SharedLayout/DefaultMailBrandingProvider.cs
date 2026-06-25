using CK.AppIdentity;
using CK.Core;
using System.IO;

namespace CK.Mail.SharedLayout;

/// <summary>
/// Default <see cref="IMailBrandingProvider"/> — reads brand values from
/// <c>CK-AppIdentity:Local:MailBranding:*</c> once at construction time and
/// caches them into a single <see cref="IMailBranding"/> IPoco instance.
/// </summary>
/// <remarks>
/// Every field defaults to an empty string when the config key is absent.
/// <c>HeaderBackgroundColor</c> defaults to <c>#1a1c20</c> (dark slate) so
/// the layout doesn't render with an entirely transparent header band when
/// no config is supplied.
/// </remarks>
public sealed class DefaultMailBrandingProvider : IMailBrandingProvider
{
    readonly IMailBranding _branding;

    /// <summary>
    /// Reads brand values from <c>CK-AppIdentity:Local:MailBranding:*</c>.
    /// </summary>
    public DefaultMailBrandingProvider( PocoDirectory pocoDirectory, IApplicationIdentityService appIdentity )
    {
        var section = appIdentity.LocalConfiguration.Configuration.TryGetSection( "MailBranding" );
        _branding = pocoDirectory.Create<IMailBranding>( b =>
        {
            b.LogoUrl = section?["LogoUrl"] ?? string.Empty;
            b.BrandName = section?["BrandName"] ?? string.Empty;
            b.PrimaryColor = section?["PrimaryColor"] ?? string.Empty;
            b.PrimaryColorHover = section?["PrimaryColorHover"] ?? string.Empty;
            b.HeaderBackgroundColor = section?["HeaderBackgroundColor"] ?? "#1a1c20";
            b.FooterAddress = section?["FooterAddress"] ?? string.Empty;
            b.FooterPhone = section?["FooterPhone"] ?? string.Empty;
            b.FooterEmail = section?["FooterEmail"] ?? string.Empty;
        } );
    }

    /// <inheritdoc />
    public IMailBranding GetBranding() => _branding;

    /// <inheritdoc />
    public Stream? OpenLogo() => null;

    /// <inheritdoc />
    public string LogoContentId => string.Empty;

    /// <inheritdoc />
    public string LogoContentType => string.Empty;
}
