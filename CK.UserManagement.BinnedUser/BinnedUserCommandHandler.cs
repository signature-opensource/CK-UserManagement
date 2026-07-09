using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.BinnedUser;

/// <summary>
/// Command handler for the BinnedUser package: the workspace-scoped archive/restore commands. Business
/// logic only (admin authority is enforced by <c>AdminCommandValidator</c>), structured monitor
/// logging, defensive try/catch and translatable answers. The <c>BinDate</c>-aware workspace-user
/// listing lives in its own <see cref="BinnedWorkspaceUsersHandler"/> so it can be superseded
/// independently.
/// </summary>
public class BinnedUserCommandHandler : IAutoService
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
}
