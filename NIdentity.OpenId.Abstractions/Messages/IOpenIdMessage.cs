using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    public interface IOpenIdMessage : IReadOnlyDictionary<string, StringValues>
    {
        bool TryLoad(string parameterName, StringValues stringValues, out ValidationResult result);
    }
}
