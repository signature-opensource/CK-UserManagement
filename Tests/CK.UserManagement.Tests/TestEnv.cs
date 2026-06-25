using CK.Core;
using CK.DB.Acl;
using CK.DB.Actor;
using CK.DB.Actor.ActorEMail;
using CK.DB.User.NamedUser;
using CK.DB.User.UserPassword;
using CK.DB.Zone;
using CK.SqlServer;
using CK.Testing;
using Dapper;
using static CK.Testing.MonitorTestHelper;
using GroupTable = CK.DB.Zone.GroupTable;

namespace CK.UserManagement.Tests;

/// <summary>
/// Shared engine + service graph + database fixture for the user-management backend tests.
/// <para>
/// The CK engine is built once for the whole assembly (see <see cref="AssemblyFixture"/>). Because
/// the handlers/services are invoked directly (not through the Cris HTTP/background pipeline) there is
/// no authenticated ambient context to honor: the tests set <c>ActorId</c>/<c>CurrentWorkspaceId</c>
/// straight onto the command pocos. The services are constructed by hand from the real objects
/// obtained from the <see cref="IStObjMap"/>, with the e-mail dispatch replaced by
/// <see cref="FakeUserManagementMailer"/>.
/// </para>
/// <para>
/// A dedicated workspace is created with two users: <see cref="AdminUserId"/> (granted workspace-admin
/// authority via an ACL grant of 127 on the workspace AclId) and <see cref="MemberUserId"/> (a plain
/// member). A spare group (<see cref="WorkspaceGroupId"/>) lives in the workspace zone for the
/// group-assignment tests.
/// </para>
/// </summary>
public sealed class TestEnv
{
    public required IStObjMap Map { get; init; }
    public required PocoDirectory PocoDirectory { get; init; }

    public required UserManagementService Service { get; init; }
    public required UserManagementQueries Queries { get; init; }
    public required UserManagementCommandHandler Handler { get; init; }
    public required AdminCommandValidator Validator { get; init; }
    public required CurrentCultureInfo CurrentCulture { get; init; }
    public required FakeUserManagementMailer Mailer { get; init; }

    public required UserTable UserTable { get; init; }
    public required GroupTable GroupTable { get; init; }
    public required NamedUserTable NamedUserTable { get; init; }
    public required UserPasswordTable UserPasswordTable { get; init; }
    public required CK.DB.Workspace.Package WorkspacePackage { get; init; }
    public required CK.DB.User.PreferredCulture.Package PreferredCulturePackage { get; init; }

    public required int WorkspaceId { get; init; }
    public required int AdminUserId { get; init; }
    public required int MemberUserId { get; init; }
    public required int WorkspaceGroupId { get; init; }

    /// <summary>A fresh, unique e-mail (invitations are keyed by a platform-unique target address).</summary>
    public static string NewEmail() => $"um-{Guid.NewGuid():N}@test.local";

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
            "CK.DB.UserInvitation",
            "CK.DB.Workspace",
            "CK.DB.Zone",
            "CK.DB.Globalization",
            "CK.UserManagement",
            "CK.SqlServer.Transaction"
        ] );
        // FakeUserManagementMailer lives in this test assembly: register it so the engine picks up the
        // [ReplaceAutoService] substitution of the real UserManagementMailer.
        configuration.FirstBinPath.Types.Add( typeof( FakeUserManagementMailer ) );

        var engineRes = await configuration.RunSuccessfullyAsync();
        var map = engineRes.LoadMap();

        var pocoDir = map.StObjs.Obtain<PocoDirectory>()!;
        var userTable = map.StObjs.Obtain<UserTable>()!;
        var groupTable = map.StObjs.Obtain<GroupTable>()!;
        var namedUserTable = map.StObjs.Obtain<NamedUserTable>()!;
        var pwdTable = map.StObjs.Obtain<UserPasswordTable>()!;
        var emailTable = map.StObjs.Obtain<ActorEMailTable>()!;
        var workspacePackage = map.StObjs.Obtain<CK.DB.Workspace.Package>()!;
        var workspaceTable = map.StObjs.Obtain<CK.DB.Workspace.WorkspaceTable>()!;
        var invitationPackage = map.StObjs.Obtain<CK.DB.UserInvitation.Package>()!;
        var preferredCulturePackage = map.StObjs.Obtain<CK.DB.User.PreferredCulture.Package>()!;
        var aclTable = map.StObjs.Obtain<AclTable>()!;

        var currentCulture = new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" ) );
        var mailer = new FakeUserManagementMailer();
        var queries = new UserManagementQueries( pocoDir, userTable );
        var service = new UserManagementService( pocoDir, currentCulture, invitationPackage, preferredCulturePackage,
                                                 emailTable, namedUserTable, pwdTable, groupTable, userTable,
                                                 workspacePackage, queries, mailer );
        var handler = new UserManagementCommandHandler( currentCulture );
        var validator = new AdminCommandValidator( userTable, workspaceTable, currentCulture );

        int workspaceId, adminId, memberId, groupId;
        var suffix = Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
        using( var ctx = new SqlTransactionCallContext() )
        {
            var ws = await workspaceTable.CreateWorkspaceAsync( ctx, 1, $"UMTestWS-{suffix}" );
            workspaceId = ws.WorkspaceId;

            adminId = await workspacePackage.CreateUserAsync( ctx, 1, $"UMAdmin-{suffix}", workspaceId );
            memberId = await workspacePackage.CreateUserAsync( ctx, 1, $"UMMember-{suffix}", workspaceId );

            // Make AdminUser a workspace administrator: grant level 127 (>= GrantLevel.SafeAdministrator)
            // directly on the workspace AclId so AdminCommandValidator/GetUserWorkspaceGrantLevel accept it.
            int aclId = await ctx[userTable].QuerySingleOrDefaultAsync<int>(
                "select AclId from CK.tWorkspace where WorkspaceId = @WorkspaceId;",
                new { WorkspaceId = workspaceId } );
            await aclTable.AclGrantSetAsync( ctx, 1, aclId, adminId, "UMTestWorkspaceAdmin", 127 );

            // A spare (non-operator) group inside the workspace zone for the group-assignment tests.
            groupId = await groupTable.CreateGroupAsync( ctx, 1, workspaceId );
        }

        return new TestEnv
        {
            Map = map,
            PocoDirectory = pocoDir,
            Service = service,
            Queries = queries,
            Handler = handler,
            Validator = validator,
            CurrentCulture = currentCulture,
            Mailer = mailer,
            UserTable = userTable,
            GroupTable = groupTable,
            NamedUserTable = namedUserTable,
            UserPasswordTable = pwdTable,
            WorkspacePackage = workspacePackage,
            PreferredCulturePackage = preferredCulturePackage,
            WorkspaceId = workspaceId,
            AdminUserId = adminId,
            MemberUserId = memberId,
            WorkspaceGroupId = groupId,
        };
    }
}
