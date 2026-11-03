using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement;

/// <summary>
/// Checks the objects of the workspace-admin commands against the current workspace.
/// <see cref="AdminCommandValidator"/> checks the actor. This validator checks what the command acts on.
/// A command opts in to a check with a command part.
/// </summary>
public class WorkspaceScopeValidator : IAutoService
{
    readonly UserTable _userTable;
    readonly UserManagementQueries _queries;
    readonly CurrentCultureInfo _culture;

    public WorkspaceScopeValidator( UserTable userTable, UserManagementQueries queries, CurrentCultureInfo culture )
    {
        _userTable = userTable;
        _queries = queries;
        _culture = culture;
    }

    /// <summary>
    /// Refuses the command when the actor cannot administer <see cref="ICommandWorkspaceUserTarget.UserId"/>
    /// from the current workspace. The rule is <see cref="UserTable.CheckUserAdministrationAsync"/>.
    /// </summary>
    [CommandHandlingValidator]
    public async Task ValidateUserTargetAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandWorkspaceUserTarget cmd )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        // AdminCommandValidator reports an invalid actor or workspace.
        if( actorId <= 0 || workspaceId <= 0 ) return;

        var check = (UserAdministrationCheck)await _userTable.CheckUserAdministrationAsync( ctx, actorId, workspaceId, cmd.UserId );
        if( check != UserAdministrationCheck.Allowed )
        {
            ctx.Monitor.Warn( $"The actor cannot administer this user from this workspace. (ActorId: {actorId}, WorkspaceId: {workspaceId}, UserId: {cmd.UserId}, Reason: {check})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "You cannot administer this user from this workspace.", "Admin.UserOutOfScope" ) );
        }
    }

    /// <summary>
    /// Refuses the command when a group of <see cref="ICommandWorkspaceGroups.Groups"/> is not the current
    /// workspace or a group of the current workspace. This is the same set of groups that the forms show.
    /// </summary>
    [CommandHandlingValidator]
    public async Task ValidateWorkspaceGroupsAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandWorkspaceGroups cmd )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        // AdminCommandValidator reports an invalid actor or workspace.
        if( actorId <= 0 || workspaceId <= 0 ) return;

        var outside = await _queries.GetGroupsOutsideWorkspaceAsync( ctx, workspaceId, cmd.Groups );
        if( outside.Count > 0 )
        {
            ctx.Monitor.Warn( $"Groups do not belong to the workspace. (ActorId: {actorId}, WorkspaceId: {workspaceId}, GroupIds: {string.Join( ", ", outside )})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "A group does not belong to this workspace.", "Admin.GroupOutOfScope" ) );
        }
    }
}
