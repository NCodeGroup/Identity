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
    /// <param name="context">The <see cref="IOpenIdContext"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <param name="ignoreErrors">Specifies whether errors during parsing should be ignored.</param>
    /// <returns>The newly loaded parameter.</returns>
    public virtual Parameter Load(IOpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues, bool ignoreErrors = false)
    {
        return new Parameter<StringValues>(descriptor, stringValues, stringValues);
    }

    /// <summary>
    /// Loads a <see cref="Parameter"/> given its string values and parsed value.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdContext"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The string values for the parameter.</param>
    /// <param name="parsedValue">The parsed value for the parameter.</param>
    /// <returns>The newly loaded parameter.</returns>
    public virtual Parameter<T> Load<T>(IOpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues, T? parsedValue)
    {
        return new Parameter<T>(descriptor, stringValues, parsedValue);
    }
}
