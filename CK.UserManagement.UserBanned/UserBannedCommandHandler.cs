using CK.Core;
using CK.Cris;
using CK.IO.UserManagement;
using CK.SqlServer;

namespace CK.UserManagement.UserBanned;

/// <summary>
/// Command handler for the UserBanned package: the workspace-scoped ban/unban commands. It supersedes
/// the package handlers (<c>CK.DB.User.UserBanned.Package</c>) for the sole purpose of forwarding the
/// current workspace to the transformed stored procedures — the admin authority itself is enforced by
/// <c>AdminCommandValidator</c>. Message resource names are the package ones so the semantics stay
/// aligned. The ban-aware workspace-user listing lives in its own
/// <see cref="BannedWorkspaceUsersHandler"/> so it can be superseded independently.
/// </summary>
public class UserBannedCommandHandler : IAutoService
{
    [CommandHandler]
    public async Task<ICrisBasicCommandResult> SetUserBannedAsync( ISqlCallContext ctx,
                                                                   UserMessageCollector collector,
                                                                   ISetUserBannedAdminCommand cmd,
                                                                   UserBannedPackage userBannedPackage )
    {
        int actorId = cmd.ActorId.GetValueOrDefault();
        int workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( ISetUserBannedAdminCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, UserId: {cmd.UserId}, KeyReason: {cmd.KeyReason})" ) )
        {
            var res = cmd.CreateResult();
            if( !CheckUserIdAndKeyReason( collector, cmd.UserId, cmd.KeyReason ) )
            {
                res.SetUserMessages( collector );
                return res;
            }
            try
            {
                await userBannedPackage.SetUserBannedAsync( ctx, actorId, cmd.KeyReason, cmd.UserId,
                                                            cmd.BanStartDate, cmd.BanEndDate, workspaceId );
                collector.Info( "User successfully banned.", "UserBanned.UserBanned" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                collector.Error( "User could not be banned.", "UserBanned.SetFailed" );
            }
            res.SetUserMessages( collector );
            return res;
        }
    }

    [CommandHandler]
    public async Task<ICrisBasicCommandResult> DestroyUserBannedAsync( ISqlCallContext ctx,
                                                                       UserMessageCollector collector,
                                                                       IDestroyUserBannedAdminCommand cmd,
                                                                       UserBannedPackage userBannedPackage )
    {
        int actorId = cmd.ActorId.GetValueOrDefault();
        int workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        using( ctx.Monitor.OpenInfo( $"Handling {nameof( IDestroyUserBannedAdminCommand )} command. (ActorId: {actorId}, WorkspaceId: {workspaceId}, UserId: {cmd.UserId}, KeyReason: {cmd.KeyReason})" ) )
        {
            var res = cmd.CreateResult();
            if( !CheckUserIdAndKeyReason( collector, cmd.UserId, cmd.KeyReason ) )
            {
                res.SetUserMessages( collector );
                return res;
            }
            try
            {
                await userBannedPackage.DestroyUserBannedAsync( ctx, actorId, cmd.KeyReason, cmd.UserId, workspaceId );
                collector.Info( "User banishment successfully destroyed.", "UserBanned.BanDestroyed" );
            }
            catch( Exception e )
            {
                ctx.Monitor.Error( e );
                collector.Error( "User banishment could not be destroyed.", "UserBanned.DestroyFailed" );
            }
            res.SetUserMessages( collector );
            return res;
        }
    }

    /// <summary>
    /// Validates the command parameters before any SQL round trip: the stored procedures would throw
    /// (Security.InvalidUserId / Security.InvalidNullOrEmptyKeyReason) but a user message is nicer.
    /// </summary>
    /// <returns>True if the parameters are valid.</returns>
    static bool CheckUserIdAndKeyReason( UserMessageCollector collector, int userId, string keyReason )
    {
        bool valid = true;
        if( userId <= 0 )
        {
            collector.Error( "Invalid user identifier.", "UserBanned.InvalidUserId" );
            valid = false;
        }
        if( string.IsNullOrWhiteSpace( keyReason ) )
        {
            collector.Error( "A ban reason is required.", "UserBanned.NoKeyReason" );
            valid = false;
        }
        return valid;
    }
}
