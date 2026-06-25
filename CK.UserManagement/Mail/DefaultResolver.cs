using CK.AppIdentity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.UserManagement.Mail;
public class DefaultResolver : IFrontUrlResolver
{
    // Used when no CK-AppIdentity:Local:FrontUrl is configured (e.g. dev without explicit config).
    const string DefaultFrontUrl = "http://localhost:4200";

    readonly IApplicationIdentityService _appIdentity;

    public DefaultResolver( IApplicationIdentityService appIdentity )
    {
        _appIdentity = appIdentity;
    }

    public string ResolveFrontUrl()
    {
        // FrontUrl lives at CK-AppIdentity:Local:FrontUrl (links must be absolute since invitation
        // commands may run without an HTTP request context). Falls back to a dev default.

        var configured = _appIdentity.LocalConfiguration.Configuration["FrontUrl"];
        return string.IsNullOrWhiteSpace( configured ) ? DefaultFrontUrl : configured.TrimEnd( '/' );
    }
}
