using CK.Core;
using CK.DB.Acl;
using CK.DB.Actor.ActorEMail;
using CK.DB.User.NamedUser;
using CK.DB.User.UserPassword;
using CK.DB.Zone;
using CK.SqlServer;
using CK.Testing;
using CK.UserManagement.UserInvitation.Mail;
using Dapper;
using static CK.Testing.MonitorTestHelper;
using GroupTable = CK.DB.Zone.GroupTable;

namespace CK.UserManagement.UserInvitation.Tests;

/// <summary>
/// Engine + service graph + database fixture for the invitation / registration tests. The engine spans
/// the core CK.UserManagement package plus CK.UserManagement.UserInvitation, with the e-mail dispatch
/// replaced by <see cref="FakeUserManagementMailer"/>.
/// <para>
/// The services are constructed by hand from the real objects obtained from the <see cref="IStObjMap"/>.
/// A dedicated workspace is created with an admin user (ACL grant 127) and a member user, plus a spare
/// group in the workspace zone.
/// </para>
/// </summary>
public sealed class TestEnv
{
    public required IStObjMap Map { get; init; }
    public required PocoDirectory PocoDirectory { get; init; }

    public required UserManagementService Service { get; init; }
    public required UserInvitationQueries InvitationQueries { get; init; }
    public required UserManagementQueries Queries { get; init; }
    public required UserInvitationCommandHandler Handler { get; init; }
    public required CurrentCultureInfo CurrentCulture { get; init; }
    public required FakeUserManagementMailer Mailer { get; init; }

    public required UserTable UserTable { get; init; }
    public required NamedUserTable NamedUserTable { get; init; }
    public required GroupTable GroupTable { get; init; }
    public required ActorEMailTable EmailTable { get; init; }
    public required CK.DB.Workspace.Package WorkspacePackage { get; init; }
    public required CK.DB.User.PreferredCulture.Package PreferredCulturePackage { get; init; }

    public required int WorkspaceId { get; init; }
    public required int AdminUserId { get; init; }
    public required int MemberUserId { get; init; }
    public required int WorkspaceGroupId { get; init; }

    /// <summary>A fresh, unique e-mail (invitations are keyed by a platform-unique target address).</summary>
    public static string NewEmail() => $"um-{Guid.NewGuid():N}@test.local";

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
            "CK.DB.User.UserPassword.EMailLogin",
            "CK.DB.User.NamedUser",
            "CK.DB.User.PreferredCulture",
            "CK.DB.User.UserBanned",
            "CK.DB.Actor.ActorEMail",
            "CK.DB.UserInvitation",
            "CK.DB.Workspace",
            "CK.DB.Zone",
            "CK.DB.Globalization",
            "CK.UserManagement",
            "CK.UserManagement.UserInvitation",
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
        var coreQueries = new UserManagementQueries( userTable );
        var invitationQueries = new UserInvitationQueries( pocoDir, userTable );
        var service = new UserManagementService( pocoDir, currentCulture, invitationPackage, preferredCulturePackage,
                                                 emailTable, namedUserTable, pwdTable, groupTable, userTable,
                                                 workspacePackage, invitationQueries, mailer );
        var handler = new UserInvitationCommandHandler( currentCulture );

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
            Service = service,
            InvitationQueries = invitationQueries,
            Queries = coreQueries,
            Handler = handler,
            CurrentCulture = currentCulture,
            Mailer = mailer,
            UserTable = userTable,
            NamedUserTable = namedUserTable,
            GroupTable = groupTable,
            EmailTable = emailTable,
            WorkspacePackage = workspacePackage,
            PreferredCulturePackage = preferredCulturePackage,
            WorkspaceId = workspaceId,
            AdminUserId = adminId,
            MemberUserId = memberId,
            WorkspaceGroupId = groupId,
        };
    }
}
