using CK.Core;
using CK.DB.Workspace;
using CK.IO.Admin;
using CK.SqlServer;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.UserManagement;

/// <summary>
/// Gives the level of the actor of a workspace-admin command on its current workspace. It is read once per command:
/// the validators of the command (<see cref="AdminCommandValidator"/>, the scope validators) share it.
/// </summary>
public sealed class ActorWorkspaceLevel : ISingletonAutoService
{
    readonly WorkspaceTable _workspaceTable;
    // Keyed by the command: an entry lives as long as its command. The actor and the workspace are kept to detect a
    // command that is changed and sent again.
    readonly ConditionalWeakTable<ICommandWorkspaceAdmin, Entry> _levels;

    sealed record Entry( int ActorId, int WorkspaceId, Task<byte> Level );

    /// <summary>
    /// Initializes a new <see cref="ActorWorkspaceLevel"/>.
    /// </summary>
    /// <param name="workspaceTable">The workspace table.</param>
    public ActorWorkspaceLevel( WorkspaceTable workspaceTable )
    {
        _workspaceTable = workspaceTable;
        _levels = new ConditionalWeakTable<ICommandWorkspaceAdmin, Entry>();
    }

    /// <summary>
    /// Gets the level of the actor of the command on its current workspace (0 for an invalid actor or workspace).
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="cmd">The command.</param>
    /// <returns>The grant level.</returns>
    public Task<byte> GetAsync( ISqlCallContext ctx, ICommandWorkspaceAdmin cmd )
    {
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        if( actorId <= 0 || workspaceId <= 0 ) return Task.FromResult( (byte)0 );
        if( _levels.TryGetValue( cmd, out var e ) && e.ActorId == actorId && e.WorkspaceId == workspaceId ) return e.Level;
        e = new Entry( actorId, workspaceId, _workspaceTable.GetUserWorkspaceGrantLevelAsync( ctx, actorId, workspaceId ) );
        _levels.AddOrUpdate( cmd, e );
        return e.Level;
    }

    /// <summary>
    /// Gets whether the actor of the command has SafeAdministrator (112) or more on its current workspace.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="cmd">The command.</param>
    /// <returns>True for a workspace administrator.</returns>
    public async Task<bool> IsWorkspaceAdministratorAsync( ISqlCallContext ctx, ICommandWorkspaceAdmin cmd )
    {
        return await GetAsync( ctx, cmd ).ConfigureAwait( false ) >= (byte)GrantLevel.SafeAdministrator;
    }
}
