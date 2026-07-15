using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

[TestFixture]
public class WorkspaceUserTests : UserManagementTestBase
{
    [Test]
    public async Task editing_a_user_adds_then_removes_workspace_group_membership_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        ( await Env.Queries.GetUserWorkspaceGroupIdsAsync( ctx, Env.WorkspaceId, userId ) )
            .ShouldNotContain( Env.WorkspaceGroupId );

        // Edit -> add to the spare group.
        var add = Env.PocoDirectory.Create<IEditWorkspaceUserCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.UserName = $"Edited-{Guid.NewGuid():N}".Substring( 0, 20 );
            c.FirstName = "First";
            c.LastName = "Last";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( Env.WorkspaceGroupId );
        } );
        ( await Edit( ctx, add ) ).Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.Queries.GetUserWorkspaceGroupIdsAsync( ctx, Env.WorkspaceId, userId ) )
            .ShouldContain( Env.WorkspaceGroupId );

        // Edit again with no groups -> the delta removes the membership.
        var remove = Env.PocoDirectory.Create<IEditWorkspaceUserCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.UserName = $"Edited-{Guid.NewGuid():N}".Substring( 0, 20 );
            c.FirstName = "First";
            c.LastName = "Last";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
        } );
        ( await Edit( ctx, remove ) ).Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.Queries.GetUserWorkspaceGroupIdsAsync( ctx, Env.WorkspaceId, userId ) )
            .ShouldNotContain( Env.WorkspaceGroupId );
    }

    // The core edit handler is e-mail-agnostic (UserName/names/culture/groups only).
    Task<SimpleUserMessage> Edit( ISqlTransactionCallContext ctx, IEditWorkspaceUserCommand cmd )
        => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable,
                                               Env.GroupTable, Env.PreferredCulturePackage, Env.Queries );

    const string TestPassword = "Str0ng!Pass";

    Task<SimpleUserMessage> Create( ISqlTransactionCallContext ctx, ICreateWorkspaceUserCommand cmd )
        => Env.Handler.CreateWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable,
                                                 Env.GroupTable, Env.PreferredCulturePackage,
                                                 Env.UserPasswordTable, Env.WorkspacePackage );

    ICreateWorkspaceUserCommand NewCreate( string userName, string password = TestPassword )
        => Env.PocoDirectory.Create<ICreateWorkspaceUserCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserName = userName;
            c.FirstName = "New";
            c.LastName = "User";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Password = password;
            c.Groups.Add( Env.WorkspaceGroupId );
        } );

    [Test]
    public async Task creating_a_user_directly_creates_it_adds_it_to_the_group_and_provisions_its_password_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var userName = $"created-{Guid.NewGuid():N}".Substring( 0, 20 );

        ( await Create( ctx, NewCreate( userName ) ) ).Level.ShouldBe( UserMessageLevel.Info );

        int userId = await Env.UserTable.FindByNameAsync( ctx, userName );
        userId.ShouldBeGreaterThan( 0 );

        // Added to the defined workspace group, and listed among the workspace users.
        ( await Env.Queries.GetUserWorkspaceGroupIdsAsync( ctx, Env.WorkspaceId, userId ) )
            .ShouldContain( Env.WorkspaceGroupId );
        ( await Env.Queries.GetWorkspaceUsersAsync( ctx, Env.WorkspaceId ) )
            .ShouldContain( u => u.UserId == userId );

        // The provisioned password lets the user sign in (actualLogin: false — credential check only).
        var login = await Env.UserPasswordTable.LoginUserAsync( ctx, userId, TestPassword, actualLogin: false );
        login.IsSuccess.ShouldBeTrue();
        ( await Env.UserPasswordTable.LoginUserAsync( ctx, userId, "wrong-password", actualLogin: false ) )
            .IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task creating_a_user_without_a_password_is_rejected_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var userName = $"nopwd-{Guid.NewGuid():N}".Substring( 0, 20 );

        ( await Create( ctx, NewCreate( userName, password: "" ) ) ).Level.ShouldBe( UserMessageLevel.Error );
        // The user must not have been created.
        ( await Env.UserTable.FindByNameAsync( ctx, userName ) ).ShouldBe( 0 );
    }

    [Test]
    public async Task creating_a_user_with_an_existing_name_is_rejected_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var userName = $"dup-{Guid.NewGuid():N}".Substring( 0, 20 );

        ( await Create( ctx, NewCreate( userName ) ) ).Level.ShouldBe( UserMessageLevel.Info );
        ( await Create( ctx, NewCreate( userName ) ) ).Level.ShouldBe( UserMessageLevel.Error );
    }
}
