using CK.Core;

namespace CK.Mail.SharedLayout;

/// <summary>
/// Brand values injected into the shared mail layout
/// (<c>_DefaultMailLayout.liquid</c>). Populated by
/// <see cref="IMailBrandingProvider"/> at render time and exposed to the
/// layout as <c>{{ branding.* }}</c>.
/// </summary>
/// <remarks>
/// All values are strings so operators can edit them via configuration
/// without touching code. Empty values are acceptable — the layout
/// degrades gracefully (no logo image, neutral colors, blank footer).
/// </remarks>
public interface IMailBranding : IPoco
{
    /// <summary>
    /// URL of the logo image displayed in the header.
    /// May be absolute (<c>https://cdn.example/logo.png</c>) or relative to
    /// the frontend origin (<c>/logos/brand-white.png</c>) — the render path
    /// resolves relative URLs against the current <c>frontUrl</c>.
    /// </summary>
    string LogoUrl { get; set; }

    /// <summary>
    /// Alt text for the logo and the company name displayed in the footer.
    /// </summary>
    string BrandName { get; set; }

    /// <summary>
    /// Primary brand color (hex, e.g. <c>#009a9c</c>) used for the CTA
    /// button background and link color.
    /// </summary>
    string PrimaryColor { get; set; }

    /// <summary>
    /// Hover-state color for the CTA button (hex).
    /// </summary>
    string PrimaryColorHover { get; set; }

    /// <summary>
    /// Background color of the header band (hex). A dark color works well
    /// with a white logo; a light color works with a dark logo.
    /// </summary>
    string HeaderBackgroundColor { get; set; }

    /// <summary>
    /// Company address line displayed in the footer.
    /// </summary>
    string FooterAddress { get; set; }

    /// <summary>
    /// Company phone number displayed in the footer.
    /// </summary>
    string FooterPhone { get; set; }

    /// <summary>
    /// Company email displayed in the footer (rendered as a <c>mailto:</c> link).
    /// </summary>
    string FooterEmail { get; set; }
}
