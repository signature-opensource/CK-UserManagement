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
/// skipped. Like CRIS, it runs the incoming validators first: when one of them collected an error, the
/// command is refused before the handling validators. Then it runs all the handling validators (in model
/// order): when no validator collected an error, it runs the handler.
/// </para>
/// <para>
/// A validator is registered with its type and the <c>nameof</c> of its method: renaming the method breaks
/// the compilation of the registration instead of silently skipping the validator.
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
    /// Registers a validator method of <typeparamref name="TValidator"/>.
    /// </summary>
    /// <typeparam name="TValidator">The type that defines the validator method.</typeparam>
    /// <param name="methodName">The <c>nameof</c> of the validator method.</param>
    /// <param name="run">Runs the validator on the command.</param>
    /// <returns>This pipeline.</returns>
    public CrisValidationPipeline Register<TValidator>( string methodName, Func<ISqlCallContext, UserMessageCollector, ICrisPoco, Task> run )
    {
        typeof( TValidator ).GetMethod( methodName ).ShouldNotBeNull( $"{typeof( TValidator ).Name} has no method {methodName}." );
        _validators.Add( $"{typeof( TValidator ).Name}.{methodName}", run );
        return this;
    }

    /// <summary>
    /// Registers the two methods of the <see cref="AdminCommandValidator"/>.
    /// </summary>
    /// <param name="v">The validator.</param>
    /// <returns>This pipeline.</returns>
    public CrisValidationPipeline RegisterAdminValidator( AdminCommandValidator v )
        => Register<AdminCommandValidator>( nameof( AdminCommandValidator.ValidateAdminCommandAsync ),
                     ( ctx, c, cmd ) => v.ValidateAdminCommandAsync( ctx, c, (CK.IO.Admin.ICommandWorkspaceAdmin)cmd ) )
          .Register<AdminCommandValidator>( nameof( AdminCommandValidator.ValidatePlateformAdminCommandAsync ),
                     ( ctx, c, cmd ) => v.ValidatePlateformAdminCommandAsync( ctx, c, (CK.IO.Admin.ICommandPlatformAdmin)cmd ) );

    /// <summary>
    /// Registers the methods of the <see cref="WorkspaceScopeValidator"/>.
    /// </summary>
    /// <param name="v">The validator.</param>
    /// <returns>This pipeline.</returns>
    public CrisValidationPipeline RegisterWorkspaceScopeValidator( WorkspaceScopeValidator v )
        => Register<WorkspaceScopeValidator>( nameof( WorkspaceScopeValidator.ValidateUserTargetAsync ),
                     ( ctx, c, cmd ) => v.ValidateUserTargetAsync( ctx, c, (CK.IO.UserManagement.ICommandWorkspaceUserTarget)cmd ) )
          .Register<WorkspaceScopeValidator>( nameof( WorkspaceScopeValidator.ValidateWorkspaceGroupsAsync ),
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
        List<string> GetValidators( CrisHandlerKind kind ) => model.Handlers
                              .Where( h => h.Kind == kind )
                              .Select( h => $"{h.Type.ClassType.Name}.{h.MethodName}" )
                              .Where( v => !v.StartsWith( "CrisCultureService." )
                                           && v != "CrisAuthenticationService.ValidateAuthenticatedPart" )
                              .ToList();
        var incoming = GetValidators( CrisHandlerKind.IncomingValidator );
        var handling = GetValidators( CrisHandlerKind.CommandHandlingValidator );
        var unknown = incoming.Concat( handling ).Where( v => !_validators.ContainsKey( v ) ).ToList();
        unknown.ShouldBeEmpty( $"The test pipeline does not simulate these validators of {model.PocoName}." );

        var collector = new UserMessageCollector( _culture );
        foreach( var v in incoming )
        {
            await _validators[v]( ctx, collector, cmd );
        }
        // An incoming validation error refuses the command: the handling validators don't run.
        if( collector.ErrorCount == 0 )
        {
            foreach( var v in handling )
            {
                await _validators[v]( ctx, collector, cmd );
            }
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
