using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.UserManagement.Mail;
public interface IFrontUrlResolver : ISingletonAutoService
{
    string ResolveFrontUrl();
}
