using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages.Parameters
{
    internal class OpenIdParameterLoader
    {
        public virtual bool TryLoad(string parameterName, OpenIdStringValues stringValues, OpenIdParameter parameter, out ValidationResult result)
        {
            parameter.Update(stringValues, null);
            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}
