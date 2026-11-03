-- SetupConfig: { "Requires": [ "CK.fAclGrantLevel", "CK.fIsUserPlatformAdmin" ] }
--
-- Returns 0 when @ActorId can administer @UserId from @WorkspaceId. Otherwise returns the reason:
-- 1: invalid parameters. 2: @UserId is not a member of @WorkspaceId.
-- 3: @UserId is a platform administrator or a member of the System group.
-- 4: @UserId is a member of a workspace where @ActorId is below SafeAdministrator (112).
-- A platform administrator can administer any user.
--
-- A user has no ACL. The ACLs of its workspaces are its security contexts, so an action on the user
-- account needs SafeAdministrator on all of them. The membership in @WorkspaceId is necessary: without
-- it, a user that is a member of no workspace passes the last check.
create function CK.fUserAdministrationCheck
(
    @ActorId int,
    @WorkspaceId int,
    @UserId int
)
returns tinyint
as
begin
    if @ActorId <= 0 or @WorkspaceId <= 0 or @UserId <= 1 return 1;
    if CK.fIsUserPlatformAdmin( @ActorId ) = 1 return 0;
    if not exists( select 1 from CK.tActorProfile where ActorId = @UserId and GroupId = @WorkspaceId ) return 2;
    -- CK.fIsUserPlatformAdmin also returns 1 for the members of the System group.
    if CK.fIsUserPlatformAdmin( @UserId ) = 1 return 3;
    if exists( select 1
                 from CK.tActorProfile ap
                     inner join CK.tWorkspace w on w.WorkspaceId = ap.GroupId
                 where ap.ActorId = @UserId
                   and w.WorkspaceId > 0
                   and CK.fAclGrantLevel( @ActorId, w.AclId ) < 112 ) return 4;
    return 0;
end
