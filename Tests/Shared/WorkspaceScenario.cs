using CK.Core;
using CK.DB.Acl;
using CK.SqlServer;
using Dapper;

namespace CK.UserManagement.Testing;

/// <summary>
/// Builds workspaces, administrators and users for the tests of the workspace scope rules.
/// All the changes are done as the System actor (1). Each call makes new objects with unique names, so
/// the tests do not share state.
/// </summary>
public sealed class WorkspaceScenario
{
    /// <summary>The platform administrators group.</summary>
    public const int PlatformAdminGroupId = 2;

    /// <summary>The System group.</summary>
    public const int SystemGroupId = 1;

    readonly UserTable _userTable;
    readonly CK.DB.Workspace.WorkspaceTable _workspaceTable;
    readonly CK.DB.Workspace.Package _workspacePackage;
    readonly CK.DB.Zone.ZoneTable _zoneTable;
    readonly CK.DB.Zone.GroupTable _groupTable;
    readonly AclTable _aclTable;

    public WorkspaceScenario( IStObjMap map )
    {
        _userTable = map.StObjs.Obtain<UserTable>()!;
        _workspaceTable = map.StObjs.Obtain<CK.DB.Workspace.WorkspaceTable>()!;
        _workspacePackage = map.StObjs.Obtain<CK.DB.Workspace.Package>()!;
        _zoneTable = map.StObjs.Obtain<CK.DB.Zone.ZoneTable>()!;
        _groupTable = map.StObjs.Obtain<CK.DB.Zone.GroupTable>()!;
        _aclTable = map.StObjs.Obtain<AclTable>()!;
    }

    static string Unique( string prefix ) => $"{prefix}-{Guid.NewGuid():N}".Substring( 0, 24 );

    /// <summary>Creates a workspace and its administrator (level 127 on the workspace ACL).</summary>
    public async Task<(int WorkspaceId, int AdminId)> NewWorkspaceWithAdminAsync( ISqlCallContext ctx )
    {
        var ws = await _workspaceTable.CreateWorkspaceAsync( ctx, 1, Unique( "ScopeWS" ) );
        int admin = await NewUserAsync( ctx, ws.WorkspaceId );
        await GrantWorkspaceAdminAsync( ctx, ws.WorkspaceId, admin );
        return (ws.WorkspaceId, admin);
    }

    /// <summary>
    /// Creates a user. The user is a member of each workspace in <paramref name="workspaceIds"/>.
    /// With no workspace, the user is a member of no workspace.
    /// </summary>
    public async Task<int> NewUserAsync( ISqlCallContext ctx, params int[] workspaceIds )
    {
        int userId = await _workspacePackage.CreateUserAsync( ctx, 1, Unique( "ScopeUser" ), workspaceIds.FirstOrDefault() );
        foreach( var w in workspaceIds.Skip( 1 ) )
        {
            await _zoneTable.AddMemberAsync( ctx, 1, w, userId );
        }
        return userId;
    }

    /// <summary>Gives level 127 on the workspace ACL to the user.</summary>
    public async Task GrantWorkspaceAdminAsync( ISqlCallContext ctx, int workspaceId, int userId )
    {
        int aclId = await ctx[_userTable].QuerySingleAsync<int>( "select AclId from CK.tWorkspace where WorkspaceId = @W;", new { W = workspaceId } );
        await _aclTable.AclGrantSetAsync( ctx, 1, aclId, userId, "ScopeTestWorkspaceAdmin", 127 );
    }

    /// <summary>Adds the user to the platform administrators group.</summary>
    public Task MakePlatformAdminAsync( ISqlCallContext ctx, int userId )
        => _groupTable.AddMemberAsync( ctx, 1, PlatformAdminGroupId, userId, autoAddMemberInZone: true );

    /// <summary>Adds the user to the System group.</summary>
    public Task MakeSystemMemberAsync( ISqlCallContext ctx, int userId )
        => _groupTable.AddMemberAsync( ctx, 1, SystemGroupId, userId, autoAddMemberInZone: true );

    /// <summary>Creates a group in the workspace and returns its identifier.</summary>
    public Task<int> NewGroupAsync( ISqlCallContext ctx, int workspaceId ) => _groupTable.CreateGroupAsync( ctx, 1, workspaceId );

    public Task<bool> IsPlatformAdminAsync( ISqlCallContext ctx, int userId )
        => ctx[_userTable].QuerySingleAsync<bool>( "select CK.fIsUserPlatformAdmin( @U );", new { U = userId } );

    public async Task<bool> IsMemberAsync( ISqlCallContext ctx, int actorId, int groupId )
        => await ctx[_userTable].QuerySingleAsync<int>( "select count(*) from CK.tActorProfile where ActorId = @A and GroupId = @G;",
                                                         new { A = actorId, G = groupId } ) > 0;

    public Task<string> UserNameOfAsync( ISqlCallContext ctx, int userId )
        => ctx[_userTable].QuerySingleAsync<string>( "select UserName from CK.tUser where UserId = @U;", new { U = userId } );

    public Task<byte[]?> PasswordHashOfAsync( ISqlCallContext ctx, int userId )
        => ctx[_userTable].QuerySingleOrDefaultAsync<byte[]?>( "select PwdHash from CK.tUserPassword where UserId = @U;", new { U = userId } );
}
