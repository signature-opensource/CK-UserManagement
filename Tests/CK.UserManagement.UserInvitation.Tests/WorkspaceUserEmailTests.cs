using CK.Core;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;
using EditWorkspaceUserCommand = CK.IO.UserManagement.UserInvitation.IEditWorkspaceUserCommand;
using WorkspaceUser = CK.IO.UserManagement.UserInvitation.IWorkspaceUser;

namespace CK.UserManagement.UserInvitation.Tests;

/// <summary>
/// Workspace-user edit + listing tests for the e-mail-aware handlers provided by the UserInvitation
/// package (they supersede the core UserName-only handlers).
/// </summary>
[TestFixture]
public class WorkspaceUserEmailTests : UserInvitationTestBase
{
    // The UserInvitation edit handler edits UserName/names/culture/groups AND the primary e-mail.
    Task<SimpleUserMessage> Edit( ISqlTransactionCallContext ctx, EditWorkspaceUserCommand cmd )
        => Env.Handler.EditWorkspaceUserAsync( ctx, cmd, Env.UserTable, Env.NamedUserTable, Env.GroupTable,
                                               Env.PreferredCulturePackage, Env.EmailTable, Env.Queries, Env.InvitationQueries );

    [Test]
    public async Task editing_a_user_updates_the_nickname_and_the_primary_email_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userId = await Env.CreateWorkspaceMemberAsync( ctx );

        var newUserName = $"nick-{Guid.NewGuid():N}".Substring( 0, 20 );
        var newEmail = TestEnv.NewEmail();

        var edit = Env.PocoDirectory.Create<EditWorkspaceUserCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userId;
            c.UserName = newUserName;
            c.Email = newEmail;
            c.FirstName = "First";
            c.LastName = "Last";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( Env.WorkspaceGroupId );
        } );
        ( await Edit( ctx, edit ) ).Level.ShouldBe( UserMessageLevel.Info );

        // The nickname and the primary e-mail are two distinct, independently updated values.
        ( await Env.UserTable.FindByNameAsync( ctx, newUserName ) ).ShouldBe( userId );
        ( await Env.InvitationQueries.GetPrimaryEmailAsync( ctx, userId ) ).ShouldBe( newEmail );

        var edited = (WorkspaceUser)( await Env.InvitationQueries.GetWorkspaceUsersWithEmailAsync( ctx, Env.WorkspaceId ) )
            .Single( u => u.UserId == userId );
        edited.UserName.ShouldBe( newUserName );
        edited.Email.ShouldBe( newEmail );
    }

    [Test]
    public async Task editing_a_user_with_an_email_owned_by_another_user_is_rejected_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        int userA = await Env.CreateWorkspaceMemberAsync( ctx );
        int userB = await Env.CreateWorkspaceMemberAsync( ctx );

        var takenEmail = TestEnv.NewEmail();
        await Env.EmailTable.AddEMailAsync( ctx, Env.AdminUserId, userB, takenEmail, isPrimary: true );

        var edit = Env.PocoDirectory.Create<EditWorkspaceUserCommand>( c =>
        {
            c.ActorId = Env.AdminUserId;
            c.CurrentWorkspaceId = Env.WorkspaceId;
            c.UserId = userA;
            c.UserName = $"nick-{Guid.NewGuid():N}".Substring( 0, 20 );
            c.Email = takenEmail;
            c.FirstName = "First";
            c.LastName = "Last";
            c.ExtendedCultureId = TestEnv.FrenchExtendedCultureId;
            c.Groups.Add( Env.WorkspaceGroupId );
        } );
        ( await Edit( ctx, edit ) ).Level.ShouldBe( UserMessageLevel.Error );

        // The whole edit rolled back: userA did not steal the e-mail.
        ( await Env.InvitationQueries.GetPrimaryEmailAsync( ctx, userA ) ).ShouldBeNull();
    }
}
