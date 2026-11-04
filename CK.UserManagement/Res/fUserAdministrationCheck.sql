-- SetupConfig: { "Requires": [ "CK.fAclGrantLevel", "CK.fIsUserPlatformAdmin" ] }
--
-- Returns 0 when @ActorId can administer @UserId from @WorkspaceId. Otherwise returns the reason:
-- 1: invalid parameters. 2: @UserId is not a member of @WorkspaceId.
-- 3: @UserId is a platform administrator or a member of the System group.
-- 4: @UserId is a member of a workspace where @ActorId is below SafeAdministrator (112).
-- 5: @UserId is @ActorId: an actor manages its own account with the profile commands.
-- 6: @UserId has a higher level than @ActorId on one of its workspaces.
-- 7: @AccountTakeover is 1 (the action gives the account to the actor: password, primary e-mail) and @UserId has
--    SafeAdministrator (112) or more on one of its workspaces: only a platform administrator can do it.
-- A platform administrator can administer any user.
--
-- A user has no ACL. The ACLs of its workspaces are its security contexts, so an action on the user
-- account needs SafeAdministrator on all of them, and at least the level of the user on each of them. The
-- membership in @WorkspaceId is necessary: without it, a user that is a member of no workspace passes the
-- workspace checks.
create function CK.fUserAdministrationCheck
(
    @ActorId int,
    @WorkspaceId int,
    @UserId int,
    @AccountTakeover bit
)
returns tinyint
as
begin
    if @ActorId <= 0 or @WorkspaceId <= 0 or @UserId <= 1 return 1;
    if CK.fIsUserPlatformAdmin( @ActorId ) = 1 return 0;
    if @UserId = @ActorId return 5;
    if not exists( select 1 from CK.tActorProfile where ActorId = @UserId and GroupId = @WorkspaceId ) return 2;
    -- CK.fIsUserPlatformAdmin also returns 1 for the members of the System group.
    if CK.fIsUserPlatformAdmin( @UserId ) = 1 return 3;
    declare @Levels table( ActorLevel tinyint not null, UserLevel tinyint not null );
    insert into @Levels( ActorLevel, UserLevel )
        select CK.fAclGrantLevel( @ActorId, w.AclId ), CK.fAclGrantLevel( @UserId, w.AclId )
            from CK.tActorProfile ap
                inner join CK.tWorkspace w on w.WorkspaceId = ap.GroupId
            where ap.ActorId = @UserId
              and w.WorkspaceId > 0;
    if exists( select 1 from @Levels where ActorLevel < 112 ) return 4;
    if exists( select 1 from @Levels where UserLevel > ActorLevel ) return 6;
    if @AccountTakeover = 1 and exists( select 1 from @Levels where UserLevel >= 112 ) return 7;
    return 0;
end
