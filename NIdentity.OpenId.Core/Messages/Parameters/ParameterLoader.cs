using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal class ParameterLoader
    {
        public virtual bool TryLoad(IOpenIdMessageContext context, Parameter parameter, StringValues stringValues, out ValidationResult result)
        {
            parameter.Load(stringValues, null);
            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}
