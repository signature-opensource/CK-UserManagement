using CK.Auth;
using CK.Core;
using CK.Cris;

namespace CK.IO.Admin;

/// <summary>
/// Marker for commands that require platform-admin authority.
/// Cross-workspace — no workspace context is resolved; the validator
/// only checks the caller is a platform administrator.
/// </summary>
[CKTypeDefiner]
public interface ICommandPlatformAdmin : ICommandPart, ICommandAuthNormal
{
}
