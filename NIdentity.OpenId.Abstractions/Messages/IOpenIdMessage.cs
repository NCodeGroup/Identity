using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages
{
    public interface IOpenIdMessage : IReadOnlyDictionary<string, StringValues>
    {
        IOpenIdMessageContext? Context { get; set; }

        void LoadParameter(string parameterName, StringValues stringValues);
    }
}
