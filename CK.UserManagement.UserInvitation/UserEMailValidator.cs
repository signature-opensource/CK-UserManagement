using CK.Core;
using CK.Cris;
using CK.SqlServer;
using System;
using System.Threading.Tasks;

namespace CK.UserManagement.UserInvitation;

/// <summary>
/// Changing the primary e-mail of a user gives its account to the actor: the password can be recovered through this
/// address. This validator applies the account rule of <see cref="UserTable.CheckUserAdministrationAsync"/> to the edit
/// command that changes the primary e-mail: a user that has SafeAdministrator (112) or more on one of its workspaces
/// can only be changed by a platform administrator.
/// <para>
/// The other refusals of the rule are reported by <see cref="WorkspaceScopeValidator"/> (the edit command is a
/// <see cref="CK.IO.UserManagement.ICommandWorkspaceUserTarget"/>): this one only reports
/// <see cref="UserAdministrationCheck.ProtectedAccount"/>.
/// </para>
/// </summary>
public class UserEMailValidator : IAutoService
{
    readonly UserTable _userTable;
    readonly ActorWorkspaceLevel _actorLevel;
    readonly UserInvitationQueries _queries;
    readonly CurrentCultureInfo _culture;

    public UserEMailValidator( UserTable userTable, ActorWorkspaceLevel actorLevel, UserInvitationQueries queries, CurrentCultureInfo culture )
    {
        _userTable = userTable;
        _actorLevel = actorLevel;
        _queries = queries;
        _culture = culture;
    }

    /// <summary>
    /// Refuses the edit of a protected account (see <see cref="UserAdministrationCheck.ProtectedAccount"/>) when the
    /// command changes the primary e-mail of the user.
    /// </summary>
    [CommandHandlingValidator]
    public async Task ValidateEMailChangeAsync( ISqlCallContext ctx, UserMessageCollector collector, CK.IO.UserManagement.UserInvitation.IEditWorkspaceUserCommand cmd )
    {
        if( string.IsNullOrWhiteSpace( cmd.Email ) || !await _actorLevel.IsWorkspaceAdministratorAsync( ctx, cmd ) ) return;
        var current = await _queries.GetPrimaryEmailAsync( ctx, cmd.UserId );
        if( string.Equals( current, cmd.Email, StringComparison.OrdinalIgnoreCase ) ) return;
        var actorId = cmd.ActorId.GetValueOrDefault();
        var workspaceId = cmd.CurrentWorkspaceId.GetValueOrDefault();
        var check = (UserAdministrationCheck)await _userTable.CheckUserAdministrationAsync( ctx, actorId, workspaceId, cmd.UserId, accountTakeover: true );
        if( check == UserAdministrationCheck.ProtectedAccount )
        {
            ctx.Monitor.Warn( $"The actor cannot change the primary e-mail of this user. (ActorId: {actorId}, WorkspaceId: {workspaceId}, UserId: {cmd.UserId})" );
            collector.UserMessages.Add( WorkspaceScopeValidator.GetRefusalMessage( _culture, check ) );
        }
    }
}
