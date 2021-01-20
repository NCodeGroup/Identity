using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdParameterLoader
    {
        public static OpenIdParameterLoader Default = new();

        public virtual bool TryLoad(string parameterName, OpenIdStringValues stringValues, OpenIdParameter parameter, out ValidationResult result)
        {
            parameter.Update(stringValues, null);
            result = ValidationResult.SuccessResult;
            return true;
        }
    }
}
