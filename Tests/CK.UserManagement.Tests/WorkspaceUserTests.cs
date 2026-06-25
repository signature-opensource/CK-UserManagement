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
            c.CultureName = "fr";
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
            c.CultureName = "fr";
        } );
        ( await Edit( ctx, remove ) ).Level.ShouldBe( UserMessageLevel.Info );
        ( await Env.Queries.GetUserWorkspaceGroupIdsAsync( ctx, Env.WorkspaceId, userId ) )
            .ShouldNotContain( Env.WorkspaceGroupId );
    }

    Task<SimpleUserMessage> Edit( ISqlTransactionCallContext ctx, IEditWorkspaceUserCommand cmd )
        => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.UserPasswordTable,
                                               Env.GroupTable, Env.PreferredCulturePackage, Env.Queries );

    [Test]
    public async Task archiving_then_restoring_a_user_toggles_its_bindate_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        var archiveCollector = new UserMessageCollector( Env.CurrentCulture );
        var archive = Env.PocoDirectory.Create<IArchiveUsersAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserIds.Add( userId );
        } );
        await Env.Handler.ArchiveUsersAsync( ctx, archiveCollector, archive, Env.UserTable );
        archiveCollector.ErrorCount.ShouldBe( 0 );

        var archived = await Env.Queries.GetWorkspaceUsersAsync( ctx, Env.WorkspaceId );
        archived.Single( u => u.UserId == userId ).BinDate.ShouldNotBeNull();

        var restoreCollector = new UserMessageCollector( Env.CurrentCulture );
        var restore = Env.PocoDirectory.Create<IRestoreUsersAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserIds.Add( userId );
        } );
        await Env.Handler.RestoreUsersAsync( ctx, restoreCollector, restore, Env.UserTable );
        restoreCollector.ErrorCount.ShouldBe( 0 );

        var restored = await Env.Queries.GetWorkspaceUsersAsync( ctx, Env.WorkspaceId );
        restored.Single( u => u.UserId == userId ).BinDate.ShouldBeNull();
    }

    [Test]
    public async Task archiving_with_no_user_ids_reports_an_error_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var collector = new UserMessageCollector( Env.CurrentCulture );
        var archive = Env.PocoDirectory.Create<IArchiveUsersAdminCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
        } );

        await Env.Handler.ArchiveUsersAsync( ctx, collector, archive, Env.UserTable );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
    }
}
