using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal class ParameterLoader
    {
        public virtual void Load(IOpenIdMessageContext context, Parameter parameter, StringValues stringValues)
        {
            parameter.Update(stringValues, null);
        }
    }
}
