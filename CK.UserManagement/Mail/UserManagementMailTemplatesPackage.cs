using CK.Template.Fluid;

namespace CK.UserManagement.Mail;

/// <summary>
/// Declares <c>CK.UserManagement</c> as a contributor of Fluid templates: its
/// <c>Res/Templates/*.liquid</c> resources (the user-invitation e-mail) are bound to their
/// models via the <c>[FluidTemplate]</c> attributes (see <see cref="IUserInvitationModel"/>).
/// </summary>
[FluidTemplatePackage]
public sealed class UserManagementMailTemplatesPackage : FluidTemplatePackage
{
}
