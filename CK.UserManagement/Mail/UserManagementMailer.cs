using CK.AppIdentity;
using CK.Core;
using CK.IO.UserManagement;
using CK.Mail.SharedLayout;
using CK.Mailer;
using CK.Template.Fluid;

namespace CK.UserManagement.Mail;

/// <summary>
/// Default <see cref="IUserManagementMailer"/>: renders the <c>UserInvitation</c> Fluid templates
/// (subject + HTML body) for the requested culture and dispatches through the
/// CK-AppIdentity-configured <see cref="IDefaultEmailSender"/> (SMTP / pickup directory / no-send
/// are all driven by <c>CK-AppIdentity:Local:EmailSender</c>). The sender address comes from that
/// same configuration.
/// </summary>
public class UserManagementMailer : IUserManagementMailer
{
    readonly IFluidTemplateService _fluid;
    readonly PocoDirectory _pocoDir;
    readonly IFrontUrlResolver _frontUrlResolver;

    readonly IDefaultEmailSender _emailSender;
    readonly IMailBrandingProvider _brandingProvider;
    readonly string _frontUrl;

    public UserManagementMailer( IFluidTemplateService fluid,
                                 PocoDirectory pocoDir,
                                 IFrontUrlResolver frontUrlResolver,
                                 IDefaultEmailSender emailSender,
                                 IMailBrandingProvider brandingProvider
                               )
    {
        _fluid = fluid;
        _pocoDir = pocoDir;
        _frontUrlResolver = frontUrlResolver;
        _emailSender = emailSender;
        _brandingProvider = brandingProvider;

        _frontUrl = _frontUrlResolver.ResolveFrontUrl();
    }

    public async Task SendUserInvitationAsync( IActivityMonitor monitor, string destination, string token, string cultureName )
    {
        var culture = NormalizedCultureInfo.EnsureNormalizedCultureInfo( string.IsNullOrEmpty( cultureName ) ? "fr" : cultureName );
        var model = _pocoDir.Create<IUserInvitationModel>( m =>
        {
            m.FrontUrl = _frontUrl;
            m.Token = token;
        } );

        var subject = await _fluid.RenderAsync( "UserInvitation.Subject", culture, model );
        var (html, inlineLogo) = await RenderWithLayoutAsync( "UserInvitation.Body", culture, model, _frontUrl );

        await SendAsync( monitor, destination, subject, html, inlineLogo );

        monitor.Info( $"User invitation e-mail sent. (Email: {destination}, Culture: {culture.Name})" );
    }


    /// <summary>
    /// Renders a body template, then wraps the resulting HTML inside the
    /// shared <c>_DefaultMailLayout.liquid</c> chrome from CK.Mail.SharedLayout.
    /// Brand values (<see cref="IMailBranding"/>) flow as an ambient binding
    /// into the body render so the CTA button color tracks the tenant brand,
    /// and as a model field into the layout render so header/footer chrome
    /// picks them up directly.
    /// <para>
    /// When <see cref="IMailBrandingProvider.OpenLogo"/> returns a stream, the
    /// logo ships as an inline <c>cid:</c> MIME attachment (bypasses Outlook's
    /// remote-image blocking) and the layout's <c>logoUrl</c> becomes
    /// <c>cid:&lt;ContentId&gt;</c>. Otherwise it falls back to
    /// <see cref="IMailBranding.LogoUrl"/> (absolute kept as-is, relative
    /// prepended with <paramref name="frontUrl"/>).
    /// </para>
    /// </summary>
    /// <returns>A tuple with the rendered HTML and an optional inline logo
    /// attachment that the caller must thread into the outgoing mail and
    /// dispose after send.</returns>
    async Task<(string html, Attachment? inlineLogo)> RenderWithLayoutAsync( string bodyTemplateName,
                                                                             NormalizedCultureInfo culture,
                                                                             object model,
                                                                             string frontUrl )
    {
        var branding = _brandingProvider.GetBranding();
        var ambient = new Dictionary<string, object> { ["branding"] = branding };

        var bodyHtml = await _fluid.RenderAsync( bodyTemplateName, culture, model, ambient );

        Attachment? inlineLogo = null;
        string logoUrl;
        var logoStream = _brandingProvider.OpenLogo();
        if( logoStream is not null )
        {
            inlineLogo = new Attachment
            {
                IsInline = true,
                ContentId = _brandingProvider.LogoContentId,
                ContentType = _brandingProvider.LogoContentType,
                Filename = "logo.png",
                Data = logoStream,
            };
            logoUrl = "cid:" + _brandingProvider.LogoContentId;
        }
        else
        {
            logoUrl = ResolveLogoUrl( branding.LogoUrl, frontUrl );
        }

        var layoutModel = new { bodyHtml, frontUrl, logoUrl, branding };
        var html = await _fluid.RenderAsync( "_DefaultMailLayout", culture, layoutModel );
        return (html, inlineLogo);
    }

    /// <summary>
    /// Resolves <see cref="IMailBranding.LogoUrl"/> against the current
    /// <paramref name="frontUrl"/>. Absolute URLs are returned verbatim; a
    /// relative path (<c>/logos/...</c>) gets <paramref name="frontUrl"/>
    /// prepended; an empty value remains empty (layout hides the image).
    /// </summary>
    static string ResolveLogoUrl( string logoUrl, string frontUrl )
    {
        if( string.IsNullOrEmpty( logoUrl ) ) return string.Empty;
        if( logoUrl.Contains( "://", StringComparison.Ordinal ) ) return logoUrl;
        return string.IsNullOrEmpty( frontUrl ) ? logoUrl : frontUrl + logoUrl;
    }

    async Task SendAsync( IActivityMonitor monitor,
                         string destination,
                         string subject,
                         string htmlBody,
                         Attachment? inlineLogo )
    {
        var message = new SimpleEmail { Subject = subject, HtmlBody = htmlBody };
        message.To( destination );
        if( inlineLogo is not null )
        {
            message.AddAttach( inlineLogo );
        }
        try
        {
            await _emailSender.SendAsync( monitor, message );
        }
        finally
        {
            inlineLogo?.Data?.Dispose();
        }
    }
}
