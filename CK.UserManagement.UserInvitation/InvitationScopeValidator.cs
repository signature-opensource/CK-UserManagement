using CK.Core;
using CK.Cris;
using CK.DB.Workspace;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// Checks the invitations of the workspace-admin commands against the current workspace. An invitation
/// belongs to a workspace when one of its groups is in that workspace. This is the rule of the workspace
/// pending list (<see cref="UserInvitationQueries.GetPendingInvitationsAsync"/>).
/// <para>
/// The check runs only for an actor that has SafeAdministrator (112) or more on the current workspace.
/// For another actor, <see cref="AdminCommandValidator"/> refuses the command, and an answer from this
/// check would tell the actor if an invitation exists in another workspace.
/// </para>
/// </summary>
public class InvitationScopeValidator : IAutoService
{
    readonly UserTable _userTable;
    readonly ActorWorkspaceLevel _actorLevel;
    readonly UserInvitationQueries _queries;
    readonly CurrentCultureInfo _culture;

    public InvitationScopeValidator( UserTable userTable, ActorWorkspaceLevel actorLevel, UserInvitationQueries queries, CurrentCultureInfo culture )
    {
        _userTable = userTable;
        _actorLevel = actorLevel;
        _queries = queries;
        _culture = culture;
    }

    /// <summary>
    /// Refuses the command when an invitation of <see cref="ICommandWorkspaceInvitations.Invitations"/> does not
    /// belong to the current workspace. A platform administrator can act on every invitation.
    /// </summary>
    [CommandHandlingValidator]
    public async Task ValidateInvitationsAsync( ISqlCallContext ctx, UserMessageCollector collector, ICommandWorkspaceInvitations cmd )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        if( !await _actorLevel.IsWorkspaceAdministratorAsync( ctx, cmd ) ) return;
        if( await _userTable.IsUserPlatformAdminAsync( ctx, actorId ) ) return;

        var outside = await _queries.GetEmailsOutsideWorkspaceAsync( ctx, workspaceId, cmd.Invitations.Select( i => i.Email ) );
        if( outside.Count > 0 )
        {
            ctx.Monitor.Warn( $"Invitations do not belong to the workspace. (ActorId: {actorId}, WorkspaceId: {workspaceId}, Emails: {string.Join( ", ", outside )})" );
            collector.UserMessages.Add( _culture.ErrorMessage( "An invitation does not belong to this workspace.", "Admin.InvitationOutOfScope" ) );
        }
    }
}
