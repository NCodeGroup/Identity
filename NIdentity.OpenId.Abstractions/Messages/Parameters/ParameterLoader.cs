using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parameters
{
    public class ParameterLoader
    {
        public static ParameterLoader Default { get; } = new();

        public virtual void Load(IOpenIdMessageContext context, Parameter parameter, StringValues stringValues)
        {
            parameter.Update(stringValues, null);
        }
    }
}
