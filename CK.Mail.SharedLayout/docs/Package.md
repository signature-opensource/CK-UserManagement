The shared layout of the e-mails: a Fluid template with a header, the body and a footer, branded
by configuration.

The `IMailBrandingProvider` supplies the `IMailBranding` values of the layout; the `DefaultMailBrandingProvider` reads
them from the `CK-AppIdentity:Local:MailBranding` configuration section. `MailSharedLayoutPackage` declares the
templates (`_DefaultMailLayout`).
