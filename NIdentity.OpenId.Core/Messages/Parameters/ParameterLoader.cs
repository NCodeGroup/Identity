using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal class ParameterLoader
    {
        public virtual bool TryLoad(ParameterStore parameter, StringValues stringValues, out ValidationResult result)
        {
            parameter.Update(stringValues, null);
            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}
