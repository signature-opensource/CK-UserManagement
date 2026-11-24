The commands of the workspace user management: list, create and edit the users of a workspace, and force the reset of
their password.

The commands are `IGetWorkspaceUsersQCommand`, `IGetPlatformUsersQCommand`, `IGetUserCreationDataQCommand`,
`ICreateWorkspaceUserCommand`, `IGetWorkspaceUserEditDataQCommand`, `IEditWorkspaceUserCommand` and
`IForceResetUserPasswordCommand`. Their command parts (`ICommandWorkspaceUserTarget`, `ICommandWorkspaceUserAccount`,
`ICommandWorkspaceGroups`) state what an administrator can do to a user. They are handled by CK.UserManagement.
