using CK.Core;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Shouldly;
using System.Collections.Generic;

namespace CK.UserManagement.UserInvitation.Tests;

[TestFixture]
public class UserInvitationOptionTests
{
    static IConfiguration Section( params (string Key, string Value)[] values )
    {
        var data = new Dictionary<string, string?>();
        foreach( var (k, v) in values ) data["UserInvitation:" + k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection( data ).Build().GetSection( "UserInvitation" );
    }

    [Test]
    public void the_Sender_is_optional()
    {
        new UserInvitationOption( null ).Sender.ShouldBeNull();
        new UserInvitationOption( Section( ("Sender", "Support") ) ).Sender.ShouldBe( "Support" );
        new UserInvitationOption( Section( ("Sender", "") ) ).Sender.ShouldBeNull();
    }

    [Test]
    public void an_unknown_key_throws()
    {
        Util.Invokable( () => new UserInvitationOption( Section( ("Sendr", "Support") ) ) )
            .ShouldThrow<CKException>().Message.ShouldBe( "Unknown \"Sendr\": the keys are Sender." );
    }
}
