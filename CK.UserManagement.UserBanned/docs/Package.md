Server side of the banishments in the workspace user management: the handlers of the CK.IO.UserManagement.UserBanned
commands and the workspace user list with the banishments. It runs where the database is.

`UserBannedCommandHandler` bans and unbans a user in the current workspace (the transformed `CK.sUserBannedSet` and
`CK.sUserBannedDestroy` procedures); `BannedWorkspaceUsersHandler` lists the workspace users with their banishments.
