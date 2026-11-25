using CK.Core;
using CK.Setup;
using NUnit.Framework;
using Shouldly;
using System.Linq;

namespace CK.UserManagement.UserInvitation.Tests;

/// <summary>
/// The "UserInvitation" option of the AuthCenter: its configuration model.
/// </summary>
[TestFixture]
public class UserInvitationOptionTests
{
    static UserInvitationOptionConfiguration? Read( string json, out string[] errors )
    {
        var collector = new UserMessageCollector( new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.CodeDefault ) );
        ConfigurationModelReader.TryRead<UserInvitationOptionConfiguration>( ImmutableConfigurationSection.CreateFromJson( "Test", "UserInvitation", json ),
                                                                             "UserInvitation",
                                                                             collector,
                                                                             out var model );
        errors = collector.UserMessages.Where( m => m.Level == UserMessageLevel.Error ).Select( m => m.Text ).ToArray();
        return model;
    }

    [Test]
    public void the_Sender_is_optional()
    {
        new UserInvitationOption( new UserInvitationOptionConfiguration() ).Sender.ShouldBeNull();
        new UserInvitationOption( Read( """{ "Sender": "Support" }""", out _ ).ShouldNotBeNull() ).Sender.ShouldBe( "Support" );
        new UserInvitationOption( Read( """{ "Sender": "" }""", out _ ).ShouldNotBeNull() ).Sender.ShouldBeNull();
    }

    [Test]
    public void an_unknown_key_is_an_error()
    {
        Read( """{ "Sendr": "Support" }""", out var errors ).ShouldBeNull();
        errors.ShouldHaveSingleItem().ShouldStartWith( "Unknown key 'Test:UserInvitation:Sendr'" );
    }
}
