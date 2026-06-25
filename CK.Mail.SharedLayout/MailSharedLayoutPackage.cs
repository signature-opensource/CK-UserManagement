using CK.Template.Fluid;

namespace Signature.Mail.Branding;

/// <summary>
/// Declares this assembly as a contributor of Signature-branded Fluid mail
/// chrome — the shared <c>_MailLayout.{fr,en}.liquid</c> template used by every
/// outgoing user-facing email (header with logo, body slot, footer with
/// company info).
/// </summary>
/// <remarks>
/// <para>
/// Ships only layout/partial templates — no bound IPoco models. This is the
/// canonical reason for <see cref="FluidTemplatePackage"/>: a layout-only
/// assembly has no <c>[FluidTemplate]</c>-decorated types to anchor discovery,
/// so it declares itself explicitly here.
/// </para>
/// <para>
/// Consumers (ODLM.OneBoard.App today — future: <c>CK.IO.User.UserPassword</c>'s
/// Fluid-backed mailer impl, any other mail-sending host) reference this
/// package via <c>&lt;ProjectReference&gt;</c> and get the shared layout
/// automatically — body templates just call <c>{{ bodyHtml }}</c> through the
/// catalog-resolved <c>_MailLayout</c> by name.
/// </para>
/// </remarks>
[FluidTemplatePackage]
public sealed class SignatureMailBrandingPackage : FluidTemplatePackage
{
}
