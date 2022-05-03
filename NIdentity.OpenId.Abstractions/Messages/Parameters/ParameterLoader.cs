using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages.Parameters;

/// <summary>
/// Provides the ability load a <see cref="Parameter"/> given its values.
/// </summary>
public class ParameterLoader
{
    /// <summary>
    /// Gets a default implementation of <see cref="ParameterLoader"/> that simply returns a newly initialized <see cref="Parameter"/> object.
    /// </summary>
    public static ParameterLoader Default { get; } = new();

    /// <summary>
    /// Loads a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <returns>The newly loaded parameter.</returns>
    public virtual Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues)
    {
        return new(descriptor, stringValues);
    }

    /// <summary>
    /// Loads a <see cref="Parameter"/> given its string values and parsed value.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The string values for the parameter.</param>
    /// <param name="parsedValue">The parsed value for the parameter.</param>
    /// <returns>The newly loaded parameter.</returns>
    public virtual Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues, object parsedValue)
    {
        return new(descriptor, stringValues, parsedValue);
    }
}