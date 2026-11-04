using CK.Core;
using CK.Cris;
using CK.SqlServer;
using Shouldly;

namespace CK.UserManagement.Testing;

/// <summary>
/// The result of <see cref="CrisValidationPipeline.RunAsync(ISqlCallContext, ICrisPoco, Func{Task{string}})"/>.
/// </summary>
/// <param name="Refused">True when a validator collected an error. The handler did not run.</param>
/// <param name="ErrorResNames">The resource names of the error messages that the validators collected.</param>
/// <param name="HandlerResult">The description that the handler returned. Null when the command was refused.</param>
public sealed record CrisOutcome( bool Refused, IReadOnlyList<string> ErrorResNames, string? HandlerResult );

/// <summary>
/// Simulates the validation steps of the CRIS pipeline for tests that call the handlers directly.
/// <para>
/// The pipeline reads the validators of the command from the generated CRIS model. It fails the test
/// when the model has a validator that is not registered, so that a new validator cannot be silently
/// skipped. It runs all the registered validators in model order. When no validator collected an
/// error, it runs the handler.
/// </para>
/// <para>
/// Two incoming validators are ignored. <c>CrisCultureService</c> checks only the culture name.
/// <c>CrisAuthenticationService.ValidateAuthenticatedPart</c> checks that the actor is the authenticated
/// user: in these tests, the actor is always the authenticated user.
/// </para>
/// </summary>
public sealed class CrisValidationPipeline
{
    readonly CurrentCultureInfo _culture;
    readonly Dictionary<string, Func<ISqlCallContext, UserMessageCollector, ICrisPoco, Task>> _validators = new();

    public CrisValidationPipeline( CurrentCultureInfo culture )
    {
        _culture = culture;
    }

    /// <summary>
    /// Registers a validator.
    /// </summary>
    /// <param name="validator">The name of the validator: "&lt;ClassName&gt;.&lt;MethodName&gt;".</param>
    /// <param name="run">Runs the validator on the command.</param>
    /// <returns>This pipeline.</returns>
    public CrisValidationPipeline Register( string validator, Func<ISqlCallContext, UserMessageCollector, ICrisPoco, Task> run )
    {
        _validators.Add( validator, run );
        return this;
    }

    /// <summary>
    /// Registers the two methods of the <see cref="AdminCommandValidator"/>.
    /// </summary>
    /// <param name="v">The validator.</param>
    /// <returns>This pipeline.</returns>
    public CrisValidationPipeline RegisterAdminValidator( AdminCommandValidator v )
        => Register( "AdminCommandValidator.ValidateAdminCommandAsync",
                     ( ctx, c, cmd ) => v.ValidateAdminCommandAsync( ctx, c, (CK.IO.Admin.ICommandWorkspaceAdmin)cmd ) )
          .Register( "AdminCommandValidator.ValidatePlateformAdminCommandAsync",
                     ( ctx, c, cmd ) => v.ValidatePlateformAdminCommandAsync( ctx, c, (CK.IO.Admin.ICommandPlatformAdmin)cmd ) );

    /// <summary>
    /// Registers the methods of the <see cref="WorkspaceScopeValidator"/>.
    /// </summary>
    /// <param name="v">The validator.</param>
    /// <returns>This pipeline.</returns>
    public CrisValidationPipeline RegisterWorkspaceScopeValidator( WorkspaceScopeValidator v )
        => Register( "WorkspaceScopeValidator.ValidateUserTargetAsync",
                     ( ctx, c, cmd ) => v.ValidateUserTargetAsync( ctx, c, (CK.IO.UserManagement.ICommandWorkspaceUserTarget)cmd ) )
          .Register( "WorkspaceScopeValidator.ValidateWorkspaceGroupsAsync",
                     ( ctx, c, cmd ) => v.ValidateWorkspaceGroupsAsync( ctx, c, (CK.IO.UserManagement.ICommandWorkspaceGroups)cmd ) );

    /// <summary>
    /// Runs the validators of the command, then the handler when no validator collected an error.
    /// </summary>
    /// <param name="ctx">The call context.</param>
    /// <param name="cmd">The command.</param>
    /// <param name="handler">Runs the handler and describes its result.</param>
    /// <returns>The outcome.</returns>
    public async Task<CrisOutcome> RunAsync( ISqlCallContext ctx, ICrisPoco cmd, Func<Task<string>> handler )
    {
        var model = cmd.CrisPocoModel;
        model.IsHandled.ShouldBeTrue( $"{model.PocoName} must have a handler in this engine." );
        var validators = model.Handlers
                              .Where( h => h.Kind is CrisHandlerKind.IncomingValidator or CrisHandlerKind.CommandHandlingValidator )
                              .Select( h => $"{h.Type.ClassType.Name}.{h.MethodName}" )
                              .Where( v => !v.StartsWith( "CrisCultureService." )
                                           && v != "CrisAuthenticationService.ValidateAuthenticatedPart" )
                              .ToList();
        var unknown = validators.Where( v => !_validators.ContainsKey( v ) ).ToList();
        unknown.ShouldBeEmpty( $"The test pipeline does not simulate these validators of {model.PocoName}." );

        var collector = new UserMessageCollector( _culture );
        foreach( var v in validators )
        {
            await _validators[v]( ctx, collector, cmd );
        }
        if( collector.ErrorCount > 0 )
        {
            var errors = collector.UserMessages.Where( m => m.Level == UserMessageLevel.Error ).Select( m => m.ResName ).ToList();
            return new CrisOutcome( true, errors, null );
        }
        return new CrisOutcome( false, [], await handler() );
    }

    /// <summary>
    /// Same as <see cref="RunAsync(ISqlCallContext, ICrisPoco, Func{Task{string}})"/> for a handler that
    /// returns a <see cref="SimpleUserMessage"/>.
    /// </summary>
    public Task<CrisOutcome> RunAsync( ISqlCallContext ctx, ICrisPoco cmd, Func<Task<SimpleUserMessage>> handler )
        => RunAsync( ctx, cmd, async () => { var m = await handler(); return $"{m.Level} '{m.Message}'"; } );
}
