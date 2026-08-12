using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.UserBanned;

/// <summary>
/// Dedicated handler for the ban-aware workspace-user listing. Isolated in its own service (instead of
/// living on <see cref="UserBannedCommandHandler"/>) so an application composing several list
/// enrichments can supersede just this listing via <c>[ReplaceAutoService]</c> without dropping the
/// package's ban/unban handlers.
/// </summary>
public class BannedWorkspaceUsersHandler : IAutoService, ICommandHandler<IGetWorkspaceUsersQCommand>
{
    /// <summary>
    /// Ban-aware workspace-user listing: the core columns plus the banishments of each user (read from
    /// <c>CK.tUserBanned</c>). Supersedes
    /// <c>CK.UserManagement.UserManagementCommandHandler.GetWorkspaceUsersAsync</c>.
    /// </summary>
    [CommandHandler]
    public async Task<List<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx,
                                                                    IGetWorkspaceUsersQCommand query,
                                                                    UserBannedQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspaceUsersQCommand )} query (with bans). (WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var users = await queries.GetWorkspaceUsersWithBansAsync( ctx, workspaceId );
                return users.ToList();
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                return new();
            }
        }
    }
}
