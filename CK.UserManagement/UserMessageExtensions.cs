using CK.Core;

namespace CK.UserManagement;

/// <summary>
/// Small helpers to build the canonical <see cref="UserMessage"/> answers returned by the
/// command handlers. Mirrors the convention used across the ODLM handlers so the messages
/// stay consistent (a translatable <c>resName</c> + an English fallback text).
/// </summary>
public static class UserMessageExtensions
{
    public static UserMessage CreateGenericError( this CurrentCultureInfo culture )
        => culture.ErrorMessage( "An error occured.", "CrisError.ExceptionCaught" );

    public static UserMessage CreateInvalidActorIdError( this CurrentCultureInfo culture )
        => culture.ErrorMessage( "Please logout and relog-in, we were unable to identify you.", "CrisError.InvalidActorId" );

    public static UserMessage CreateInvalidArgumentError( this CurrentCultureInfo culture )
        => culture.ErrorMessage( "Invalid arguments.", "CrisError.ArgumentError" );
}
