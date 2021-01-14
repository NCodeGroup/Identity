using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace NCode.Identity.Logic
{
    public interface ISecurityKeyCollection : IReadOnlyCollection<SecurityKey>, IDisposable
    {
        // nothing
    }
}
