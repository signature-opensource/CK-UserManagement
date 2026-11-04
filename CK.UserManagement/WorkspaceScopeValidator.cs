using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CK.UserManagement;

/// <summary>
/// Checks the objects of the workspace-admin commands against the current workspace.
/// <see cref="AdminCommandValidator"/> checks the actor. This validator checks what the command acts on.
/// A command opts in to a check with a command part.
/// <para>
/// The checks run only for an actor that has SafeAdministrator (112) or more on the current workspace.
/// For another actor, <see cref="AdminCommandValidator"/> refuses the command, and an answer from these
/// checks would tell the actor facts about other workspaces.
/// </para>
/// </summary>
public class WorkspaceScopeValidator : IAutoService
{
    readonly UserTable _userTable;
    readonly ActorWorkspaceLevel _actorLevel;
    readonly UserManagementQueries _queries;
    readonly CurrentCultureInfo _culture;

    public WorkspaceScopeValidator( UserTable userTable, ActorWorkspaceLevel actorLevel, UserManagementQueries queries, CurrentCultureInfo culture )
    {
        _userTable = userTable;
        _actorLevel = actorLevel;
        _queries = queries;
        _culture = culture;
    }

    /// <summary>
    /// Refuses the command when the actor cannot administer <see cref="ICommandWorkspaceUserTarget.UserId"/>
    /// from the current workspace. The rule is <see cref="UserTable.CheckUserAdministrationAsync"/>: a command that
    /// gives the account to the actor is a <see cref="ICommandWorkspaceUserAccount"/>.
    /// </summary>
    [CommandHandlingValidator]
    public async Task ValidateUserTargetAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandWorkspaceUserTarget cmd )
    {
        if( !await _actorLevel.IsWorkspaceAdministratorAsync( ctx, cmd ) ) return;
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        var check = (UserAdministrationCheck)await _userTable.CheckUserAdministrationAsync( ctx, actorId, workspaceId, cmd.UserId, cmd is ICommandWorkspaceUserAccount );
        if( check != UserAdministrationCheck.Allowed )
        {
            ctx.Monitor.Warn( $"The actor cannot administer this user from this workspace. (ActorId: {actorId}, WorkspaceId: {workspaceId}, UserId: {cmd.UserId}, Reason: {check})" );
            collector.UserMessages.Add( GetRefusalMessage( _culture, check ) );
        }
    }

    /// <summary>
    /// Gets the message of a refusal of <see cref="UserTable.CheckUserAdministrationAsync"/>.
    /// </summary>
    /// <param name="culture">The current culture.</param>
    /// <param name="check">The refusal reason (not <see cref="UserAdministrationCheck.Allowed"/>).</param>
    /// <returns>The message.</returns>
    public static UserMessage GetRefusalMessage( CurrentCultureInfo culture, UserAdministrationCheck check )
    {
        return check switch
        {
            UserAdministrationCheck.SelfAdministration
                => culture.ErrorMessage( "Your own account is managed from your profile.", "Admin.SelfAdministration" ),
            UserAdministrationCheck.ProtectedAccount
                => culture.ErrorMessage( "Only a platform administrator can change the password or the e-mail of an administrator.", "Admin.ProtectedAccount" ),
            _ => culture.ErrorMessage( "You cannot administer this user from this workspace.", "Admin.UserOutOfScope" )
        };
    }

    /// <summary>
    /// Refuses the command when a group of <see cref="ICommandWorkspaceGroups.Groups"/> is not the current workspace
    /// or a group of the current workspace that the forms show (<see cref="UserManagementQueries.GetWorkspaceGroupsAsync"/>,
    /// that an application can override), or when a group grants on the workspace more than the level of the actor.
    /// </summary>
    [CommandHandlingValidator]
    public async Task ValidateWorkspaceGroupsAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandWorkspaceGroups cmd )
    {
        if( cmd.Groups.Count == 0 || !await _actorLevel.IsWorkspaceAdministratorAsync( ctx, cmd ) ) return;
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();

        var allowed = (await _queries.GetWorkspaceGroupsAsync( ctx, workspaceId )).Select( g => g.GroupId ).ToHashSet();
        allowed.Add( workspaceId );
        var outside = cmd.Groups.Where( g => !allowed.Contains( g ) ).Distinct().ToList();
        if( outside.Count > 0 )
        {
            ctx.Monitor.Warn( $"Groups do not belong to the workspace. (ActorId: {actorId}, WorkspaceId: {workspaceId}, GroupIds: {string.Join( ", ", outside )})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "A group does not belong to this workspace.", "Admin.GroupOutOfScope" ) );
            return;
        }
        var actorLevel = await _actorLevel.GetAsync( ctx, cmd );
        var levels = await _queries.GetWorkspaceGroupGrantLevelsAsync( ctx, workspaceId, cmd.Groups );
        var above = levels.Where( kv => kv.Value > actorLevel ).Select( kv => kv.Key ).ToList();
        if( above.Count > 0 )
        {
            ctx.Monitor.Warn( $"Groups grant more than the level of the actor. (ActorId: {actorId}, ActorLevel: {actorLevel}, WorkspaceId: {workspaceId}, GroupIds: {string.Join( ", ", above )})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "A group grants more rights on this workspace than yours.", "Admin.GroupAboveActorLevel" ) );
        }
    }
}
