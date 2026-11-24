Server side of the invitations in the workspace user management: the handlers of the
CK.IO.UserManagement.UserInvitation commands, the invitation e-mails and the registration of the invited users. It runs
where the database is.

`UserInvitationCommandHandler` handles the invitation and registration commands and the edit of a user with its
e-mail; `UserInvitationWorkspaceUsersHandler` lists the workspace users with their e-mail. `UserManagementMailer` sends
the invitations (Fluid templates) through the sender of the "UserInvitation" option of the AuthCenter
(`UserInvitationOption`), or its default sender.
