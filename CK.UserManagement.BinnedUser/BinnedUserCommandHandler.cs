using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.BinnedUser;

/// <summary>
/// Single command handler for the BinnedUser package. Handles the workspace-scoped archive/restore
/// commands and provides the <c>BinDate</c>-aware version of the workspace-user list command
/// (superseding the core UserName-only handler). Business logic only (admin authority is enforced by
/// <c>AdminCommandValidator</c>), structured monitor logging, defensive try/catch and translatable
/// answers.
/// </summary>
public class BinnedUserCommandHandler : IAutoService,
                                        ICommandHandler<IGetWorkspaceUsersQCommand>
{
    [CommandHandler]
    public async Task<ICrisBasicCommandResult> ArchiveUsersAsync( ISqlTransactionCallContext ctx,
                                                                  UserMessageCollector collector,
                                                                  IArchiveUsersAdminCommand cmd,
                                                                  BinnedUserPackage binnedUserPackage )
    {
        int actorId = cmd.ActorId.GetValueOrDefault();
        int workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IArchiveUsersAdminCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, Count: {cmd.UserIds.Count})" ) )
        {
            var res = cmd.CreateResult();
            if( cmd.UserIds.Count == 0 )
            {
                collector.Error( "No user identifier provided.", "BinnedUser.NoUserId" );
                res.SetUserMessages( collector );
                return res;
            }
            try
            {
                using( var transaction = ctx[binnedUserPackage].BeginTransaction() )
                {
                    foreach( var id in cmd.UserIds )
                    {
                        await binnedUserPackage.ArchiveUserAsync( ctx, actorId, id, workspaceId );
                        ctx.Monitor.Info( $"User successfully archived. (UserId: {id})" );
                    }
                    transaction.Commit();
                }
                collector.Info( $"{cmd.UserIds.Count} user(s) successfully archived.", "BinnedUser.UsersArchived" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                collector.Error( "An error occurred while archiving users.", "BinnedUser.ArchiveFailed" );
            }
            res.SetUserMessages( collector );
            return res;
        }
    }

    [CommandHandler]
    public async Task<ICrisBasicCommandResult> RestoreUsersAsync( ISqlTransactionCallContext ctx,
                                                                  UserMessageCollector collector,
                                                                  IRestoreUsersAdminCommand cmd,
                                                                  BinnedUserPackage binnedUserPackage )
    {
        int actorId = cmd.ActorId.GetValueOrDefault();
        int workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IRestoreUsersAdminCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, Count: {cmd.UserIds.Count})" ) )
        {
            var res = cmd.CreateResult();
            if( cmd.UserIds.Count == 0 )
            {
                collector.Error( "No user identifier provided.", "BinnedUser.NoUserId" );
                res.SetUserMessages( collector );
                return res;
            }
            try
            {
                using( var transaction = ctx[binnedUserPackage].BeginTransaction() )
                {
                    foreach( var id in cmd.UserIds )
                    {
                        await binnedUserPackage.RestoreUserAsync( ctx, actorId, id, workspaceId );
                        ctx.Monitor.Info( $"User successfully restored. (UserId: {id})" );
                    }
                    transaction.Commit();
                }
                collector.Info( $"{cmd.UserIds.Count} user(s) successfully restored.", "BinnedUser.UsersRestored" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                collector.Error( "An error occurred while restoring users.", "BinnedUser.RestoreFailed" );
            }
            res.SetUserMessages( collector );
            return res;
        }
    }

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
