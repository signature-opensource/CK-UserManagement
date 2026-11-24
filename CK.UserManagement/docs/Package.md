Server side of the workspace user management: the handlers of the CK.IO.UserManagement commands and the validators
that enforce what an administrator can do. It runs where the database is.

`UserManagementCommandHandler` handles the commands; `AdminCommandValidator` checks that the actor is an administrator
of the workspace (or a platform administrator) and `WorkspaceScopeValidator` checks what the command acts on (the
`CK.fUserAdministrationCheck` function).
