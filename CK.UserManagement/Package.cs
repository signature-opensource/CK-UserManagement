using CK.Core;

namespace CK.UserManagement;

[SqlPackage( Schema = "CK", ResourcePath = "Res" )]
[Versions( "1.0.0" )]
public abstract class Package : SqlPackage
{
    void StObjConstruct()
    {
    }
}
   
