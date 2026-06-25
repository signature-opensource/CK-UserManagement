using CK.Core;
using CK.Cris;
using CK.DB.Workspace;
using CK.IO.Admin;
using CK.SqlServer;

namespace CK.UserManagement;

/// <summary>
/// Guards every <see cref="ICommandWorkspaceAdmin"/> handling by ensuring the
/// caller is a workspace administrator OR a platform administrator
/// (platform admins implicitly administer every workspace).
/// </summary>
/// <remarks>
/// FIXME (incubation): when <c>CK.IO.Admin</c> is lifted to CK core
/// this validator should travel with it, depending on a generic
/// <c>IAdminContext</c> abstraction rather than ODLM's tables.
/// The validator lives in <c>ODLM.OneBoard.App</c> during incubation
/// for the same circular-reference reason as
/// <see cref="WorkspaceCommandValidator"/>.
/// </remarks>
public class AdminCommandValidator : IAutoService
{
    private readonly UserTable _userTable;
    private readonly WorkspaceTable _workspaceTable;
    private readonly CurrentCultureInfo _culture;

    public AdminCommandValidator(
        UserTable userTable,
        WorkspaceTable workspaceTable,
        CurrentCultureInfo culture )
    {
        _userTable = userTable;
        _workspaceTable = workspaceTable;
        _culture = culture;
    }

    [CommandHandlingValidator]
    public async Task ValidateAdminCommandAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandWorkspaceAdmin cmd )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        if( actorId <= 0 )
        {
            ctx.Monitor.Error( $"Invalid ActorId. (ActorId: {actorId})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "Invalid actor.", "Admin.InvalidActor" ) );
            return;
        }

        var workspaceId =  cmd.CurrentWorkspaceId.GetValueOrDefault();
        if( workspaceId <= 0 )
        {
            ctx.Monitor.Error( $"Invalid WorkspaceId. (WorkspaceId: {workspaceId})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "Invalid workspace.", "Admin.InvalidWorkSpace" ) );
            return;
        }

        var grantLevel = await _workspaceTable.GetUserWorkspaceGrantLevelAsync( ctx, actorId, workspaceId );

        if( grantLevel >= (byte)GrantLevel.SafeAdministrator )
        {
            ctx.Monitor.Info( $"User is workspace admin. (ActorId: {actorId}, WorkspaceId: {cmd.CurrentWorkspaceId})" );
            return;
        }

        ctx.Monitor.Error( $"User is neither platform nor workspace admin. (ActorId: {actorId}, WorkspaceId: {cmd.CurrentWorkspaceId})" );
        collector.UserMessages.Add( _culture.ErrorMessage( "Admin authority required.", "Admin.NotAuthorized" ) );
    }


    [CommandHandlingValidator]
    public async Task ValidatePlateformAdminCommandAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandPlatformAdmin cmd )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        if( actorId <= 0 )
        {
            ctx.Monitor.Error( $"Invalid ActorId. (ActorId: {actorId})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "Invalid actor.", "Admin.InvalidActor" ) );
            return;
        }

        if( await _userTable.IsUserPlatformAdminAsync( ctx, actorId ) )
        {
            ctx.Monitor.Info( $"User is platform admin. (ActorId: {actorId})" );
            return;
        }

        ctx.Monitor.Error( $"User is not a platformAdmin. (ActorId: {actorId}" );
        collector.UserMessages.Add( _culture.ErrorMessage( "Admin authority required.", "Admin.NotAuthorized" ) );
    }
}
