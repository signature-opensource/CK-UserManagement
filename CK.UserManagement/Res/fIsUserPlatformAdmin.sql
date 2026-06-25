-- SetupConfig: { "Requires": [ "CK.fAclGrantLevel" ] }
create function CK.fIsUserPlatformAdmin
(
    @ActorId int
)
returns bit
as
begin
    if @ActorId = 0
        return 0;

    declare @PlatformAclId int;
    select @PlatformAclId = AclId from CK.tAclConfigMemory where KeyReason = 'AdministratorsGroup';
    declare @GrantLevel int;
    select @GrantLevel = CK.fAclGrantLevel( @ActorId, @PlatformAclId );

    if @GrantLevel = 127
        return 1;

    return 0;
end
