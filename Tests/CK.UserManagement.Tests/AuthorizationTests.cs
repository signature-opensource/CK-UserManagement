using CK.Core;
using CK.IO.UserManagement;
using CK.SqlServer;
using NUnit.Framework;
using Shouldly;

namespace CK.UserManagement.Tests;

/// <summary>
/// Exercises <see cref="AdminCommandValidator"/> directly (the validator that guards every
/// <c>ICommandWorkspaceAdmin</c>). The workspace-scoped command used here is irrelevant; only the
/// actor/workspace carried by the command matters.
/// </summary>
[TestFixture]
public class AuthorizationTests : UserManagementTestBase
{
    IArchiveUsersAdminCommand Command( int actorId, int workspaceId )
        => Env.PocoDirectory.Create<IArchiveUsersAdminCommand>( c =>
        {
            c.ActorId = actorId;
            c.CurrentWorkspaceId = workspaceId;
        } );

    [Test]
    public async Task a_workspace_admin_passes_the_validator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var collector = new UserMessageCollector( Env.CurrentCulture );

        await Env.Validator.ValidateAdminCommandAsync( ctx, collector, Command( Env.AdminUserId, Env.WorkspaceId ) );

        collector.ErrorCount.ShouldBe( 0 );
    }

    [Test]
    public async Task a_plain_member_is_rejected_by_the_validator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var collector = new UserMessageCollector( Env.CurrentCulture );

        await Env.Validator.ValidateAdminCommandAsync( ctx, collector, Command( Env.MemberUserId, Env.WorkspaceId ) );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
    }

    [Test]
    public async Task an_invalid_workspace_is_rejected_by_the_validator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var collector = new UserMessageCollector( Env.CurrentCulture );

        await Env.Validator.ValidateAdminCommandAsync( ctx, collector, Command( Env.AdminUserId, 0 ) );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
    }

    [Test]
    public async Task an_invalid_actor_is_rejected_by_the_validator_Async()
    {
        using var ctx = new SqlTransactionCallContext();
        var collector = new UserMessageCollector( Env.CurrentCulture );

        await Env.Validator.ValidateAdminCommandAsync( ctx, collector, Command( 0, Env.WorkspaceId ) );

        collector.ErrorCount.ShouldBeGreaterThan( 0 );
    }
}
