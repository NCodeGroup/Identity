using System.Collections.Generic;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    public interface IOpenIdMessage : IReadOnlyDictionary<string, OpenIdStringValues>
    {
        bool TryLoad(IEnumerable<KeyValuePair<string, OpenIdStringValues>> parameters, out ValidationResult result);
    }
}
