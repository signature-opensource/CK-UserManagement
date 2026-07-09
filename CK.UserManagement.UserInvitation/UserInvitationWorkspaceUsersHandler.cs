using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// Dedicated handler for the e-mail-aware workspace-user listing. Isolated in its own service (instead
/// of living on <see cref="UserInvitationCommandHandler"/>) so an application composing several list
/// enrichments can supersede just this listing via <c>[ReplaceAutoService]</c> without dropping the
/// package's invitation / edit handlers.
/// </summary>
public class UserInvitationWorkspaceUsersHandler : IAutoService, ICommandHandler<IGetWorkspaceUsersQCommand>
{
    /// <summary>
    /// E-mail-aware workspace-user listing: same result as the core handler plus the primary e-mail.
    /// Supersedes <c>CK.UserManagement.UserManagementCommandHandler.GetWorkspaceUsersAsync</c>.
    /// </summary>
    [CommandHandler]
    public async Task<List<IWorkspaceUser>> GetWorkspaceUsersAsync( ISqlCallContext ctx,
                                                                    IGetWorkspaceUsersQCommand query,
                                                                    UserInvitationQueries queries )
    {
        var workspaceId = query.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IGetWorkspaceUsersQCommand )} query (with e-mail). (WorkspaceId: {workspaceId})" ) )
        {
            try
            {
                var users = await queries.GetWorkspaceUsersWithEmailAsync( ctx, workspaceId );
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
