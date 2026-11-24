The banishment commands of the workspace user management: `ISetUserBannedAdminCommand` and
`IDestroyUserBannedAdminCommand` ban and unban a user from the current workspace.

It extends `IWorkspaceUser` and `IPlatformUser` with the banishments of the user (`IUserBan`). The commands are handled
by CK.UserManagement.UserBanned.
