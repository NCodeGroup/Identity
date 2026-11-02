// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a default implementation of the <see cref="IParameterCollection"/> abstraction.
/// </summary>
[PublicAPI]
public class ParameterCollection : IParameterCollection
{
    private Dictionary<string, IParameter> Store { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterCollection"/> class.
    /// </summary>
    public ParameterCollection()
    {
        Store = new Dictionary<string, IParameter>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterCollection"/> class and collection of <see cref="IParameter"/> values.
    /// </summary>
    /// <param name="parameters">The collection of <see cref="IParameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="IParameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    public ParameterCollection(IEnumerable<IParameter> parameters, bool cloneParameters)
    {
        Store = parameters.ToDictionary(
            parameter => parameter.Descriptor.ParameterName,
            parameter => cloneParameters ? parameter.Clone() : parameter,
            StringComparer.Ordinal
        );
    }

    /// <inheritdoc />
    public IEnumerator<IParameter> GetEnumerator() => Store.Values.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool Contains(string parameterName) => Store.ContainsKey(parameterName);

    /// <inheritdoc />
    public bool TryGet(string parameterName, [MaybeNullWhen(false)] out IParameter parameter) =>
        Store.TryGetValue(parameterName, out parameter);

    /// <inheritdoc />
    public bool TryGet<T>(ParameterKey<T> key, [MaybeNullWhen(false)] out IParameter<T> parameter)
    {
        if (
            Store.TryGetValue(key.ParameterName, out var baseParameter)
            && baseParameter is IParameter<T> typedParameter
        )
        {
            parameter = typedParameter;
            return true;
        }

        parameter = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGet<T>(
        KnownParameter<T> knownParameter,
        [MaybeNullWhen(false)] out IParameter<T> parameter
    )
    {
        ParameterKey<T> key = knownParameter;
        return TryGet(key, out parameter);
    }

    /// <inheritdoc />
    public T? GetValueOrDefault<T>(ParameterKey<T> key) =>
        TryGet(key, out var parameter) ? parameter.ParsedValue : default;

    /// <inheritdoc />
    public T? GetValueOrDefault<T>(KnownParameter<T> knownParameter)
    {
        ParameterKey<T> key = knownParameter;
        return GetValueOrDefault(key);
    }

    /// <inheritdoc />
    public bool Remove(string parameterName) => Store.Remove(parameterName);

    /// <inheritdoc />
    public void Set(IParameter parameter) => Store[parameter.Descriptor.ParameterName] = parameter;
}
