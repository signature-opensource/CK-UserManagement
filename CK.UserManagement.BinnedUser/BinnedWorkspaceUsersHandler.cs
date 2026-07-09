using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.BinnedUser;

/// <summary>
/// Dedicated handler for the <c>BinDate</c>-aware workspace-user listing. Isolated in its own service
/// (instead of living on <see cref="BinnedUserCommandHandler"/>) so an application composing several
/// list enrichments can supersede just this listing via <c>[ReplaceAutoService]</c> without dropping
/// the package's archive/restore handlers.
/// </summary>
public class BinnedWorkspaceUsersHandler : IAutoService, ICommandHandler<IGetWorkspaceUsersQCommand>
{
    /// <summary>
    /// <c>BinDate</c>-aware workspace-user listing: the core columns plus the archive date (read from
    /// <c>CK.vUser.BinDate</c>, contributed by CK.DB.User.BinnedUser's vUser transform). Supersedes
    /// <c>CK.UserManagement.UserManagementCommandHandler.GetWorkspaceUsersAsync</c>.
    /// </summary>
    [CommandHandler]
    public async Task<List<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx,
                                                                    IGetWorkspaceUsersQCommand query,
                                                                    BinnedUserQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspaceUsersQCommand )} query (with BinDate). (WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var users = await queries.GetWorkspaceUsersWithBinDateAsync( ctx, workspaceId );
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
