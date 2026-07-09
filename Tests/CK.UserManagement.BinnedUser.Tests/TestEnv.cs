using CK.Core;
using CK.DB.Acl;
using CK.SqlServer;
using CK.Testing;
using CK.UserManagement;
using Dapper;
using static CK.Testing.MonitorTestHelper;
using GroupTable = CK.DB.Zone.GroupTable;

namespace CK.UserManagement.BinnedUser.Tests;

/// <summary>
/// Engine + service graph + database fixture for the archive/restore (BinnedUser) tests. The engine
/// spans the core CK.UserManagement package (for the workspace-user read model exposing
/// <c>IWorkspaceUser.BinDate</c>) plus CK.UserManagement.BinnedUser.
/// <para>
/// A dedicated workspace is created with an admin user (ACL grant 127) and a member user. The services
/// are constructed by hand from the real objects obtained from the <see cref="IStObjMap"/>.
/// </para>
/// </summary>
public sealed class TestEnv
{
    public required IStObjMap Map { get; init; }
    public required PocoDirectory PocoDirectory { get; init; }

    public required BinnedUserCommandHandler Handler { get; init; }
    public required BinnedUserQueries BinnedUserQueries { get; init; }
    public required CurrentCultureInfo CurrentCulture { get; init; }

    public required UserTable UserTable { get; init; }
    public required BinnedUserPackage BinnedUserPackage { get; init; }
    public required CK.DB.Workspace.Package WorkspacePackage { get; init; }

    public required int WorkspaceId { get; init; }
    public required int AdminUserId { get; init; }
    public required int MemberUserId { get; init; }
    public required int WorkspaceGroupId { get; init; }

    /// <summary>The extended culture identifier for French, used as the default culture in tests.</summary>
    public static int FrenchExtendedCultureId => NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" ).Id;

    /// <summary>Creates a brand new member user inside the test workspace and returns its id.</summary>
    public async Task<int> CreateWorkspaceMemberAsync( ISqlCallContext ctx, string? name = null )
        => await WorkspacePackage.CreateUserAsync( ctx, 1, name ?? $"UMUser-{Guid.NewGuid():N}".Substring( 0, 24 ), WorkspaceId );

    public static async Task<TestEnv> CreateAsync()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Path = TestHelper.BinFolder;
        configuration.EnsureSqlServerConfigurationAspect();

        configuration.FirstBinPath.Assemblies.AddRange( [
            "CK.Cris.Auth",
            "CK.DB.AspNet.Auth",
            "CK.DB.User.UserPassword",
            "CK.DB.User.NamedUser",
            "CK.DB.User.PreferredCulture",
            "CK.DB.User.BinnedUser",
            "CK.DB.Actor.ActorEMail",
            "CK.DB.Workspace",
            "CK.DB.Zone",
            "CK.DB.Globalization",
            "CK.UserManagement",
            "CK.UserManagement.BinnedUser",
            "CK.SqlServer.Transaction"
        ] );

        var engineRes = await configuration.RunSuccessfullyAsync();
        var map = engineRes.LoadMap();

        var pocoDir = map.StObjs.Obtain<PocoDirectory>()!;
        var userTable = map.StObjs.Obtain<UserTable>()!;
        var binnedUserPackage = map.StObjs.Obtain<BinnedUserPackage>()!;
        var groupTable = map.StObjs.Obtain<GroupTable>()!;
        var workspacePackage = map.StObjs.Obtain<CK.DB.Workspace.Package>()!;
        var workspaceTable = map.StObjs.Obtain<CK.DB.Workspace.WorkspaceTable>()!;
        var aclTable = map.StObjs.Obtain<AclTable>()!;

        var currentCulture = new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" ) );
        var binnedUserQueries = new BinnedUserQueries( binnedUserPackage );
        var handler = new BinnedUserCommandHandler();

        int workspaceId, adminId, memberId, groupId;
        var suffix = Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
        using( var ctx = new SqlTransactionCallContext() )
        {
            var ws = await workspaceTable.CreateWorkspaceAsync( ctx, 1, $"UMTestWS-{suffix}" );
            workspaceId = ws.WorkspaceId;

            adminId = await workspacePackage.CreateUserAsync( ctx, 1, $"UMAdmin-{suffix}", workspaceId );
            memberId = await workspacePackage.CreateUserAsync( ctx, 1, $"UMMember-{suffix}", workspaceId );

            int aclId = await ctx[userTable].QuerySingleOrDefaultAsync<int>(
                "select AclId from CK.tWorkspace where WorkspaceId = @WorkspaceId;",
                new { WorkspaceId = workspaceId } );
            await aclTable.AclGrantSetAsync( ctx, 1, aclId, adminId, "UMTestWorkspaceAdmin", 127 );

            groupId = await groupTable.CreateGroupAsync( ctx, 1, workspaceId );
        }

        return new TestEnv
        {
            Map = map,
            PocoDirectory = pocoDir,
            Handler = handler,
            BinnedUserQueries = binnedUserQueries,
            CurrentCulture = currentCulture,
            UserTable = userTable,
            BinnedUserPackage = binnedUserPackage,
            WorkspacePackage = workspacePackage,
            WorkspaceId = workspaceId,
            AdminUserId = adminId,
            MemberUserId = memberId,
            WorkspaceGroupId = groupId,
        };
    }
}
