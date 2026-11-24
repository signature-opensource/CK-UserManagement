The invitation commands of the workspace user management: invite users by e-mail, manage the pending invitations, and
complete the registration of an invited user.

The commands are `ICreateInvitationCommand`, `IResendInvitationsCommand`, `IDeactivateInvitationsCommand`,
`IDestroyInvitationsCommand`, `IGetWorkspacePendingInvitationsQCommand`, `IGetPlatformPendingInvitationsQCommand`,
`IGetWorkspaceInvitationDataQCommand`, `IValidateInvitationTokenCommand` and `ICompleteRegistrationCommand`. It extends
`IWorkspaceUser`, `IPlatformUser` and `IEditWorkspaceUserCommand` with the e-mail of the user, and declares the
`IUserManagementMailer` that sends the invitations. The commands are handled by CK.UserManagement.UserInvitation.
