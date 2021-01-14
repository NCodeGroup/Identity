using System.Collections.Generic;
using NCode.Identity.DataContracts;

namespace NCode.Identity.Logic
{
    public interface ISecretService
    {
        ISecurityKeyCollection LoadSecurityKeys(IEnumerable<Secret> secrets);
    }
}
