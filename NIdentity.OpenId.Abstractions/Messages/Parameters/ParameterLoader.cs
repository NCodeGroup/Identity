using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages.Parameters
{
    public class ParameterLoader
    {
        public static ParameterLoader Default { get; } = new();

        public virtual Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues)
        {
            return new(descriptor, stringValues);
        }

        public virtual Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues, object parsedValue)
        {
            return new(descriptor, stringValues, parsedValue);
        }
    }
}
