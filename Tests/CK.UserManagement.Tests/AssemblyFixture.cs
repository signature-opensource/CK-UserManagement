using NUnit.Framework;

namespace CK.UserManagement.Tests;

/// <summary>
/// Builds the shared <see cref="TestEnv"/> (CK engine + service graph + workspace/users) once for the
/// whole assembly. The CK engine build is expensive, so all fixtures reuse the same environment.
/// </summary>
[SetUpFixture]
public class AssemblyFixture
{
    public static TestEnv Env { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        Env = await TestEnv.CreateAsync();
    }
}

/// <summary>Base class giving every fixture access to the shared <see cref="TestEnv"/>.</summary>
public abstract class UserManagementTestBase
{
    protected TestEnv Env => AssemblyFixture.Env;
}
