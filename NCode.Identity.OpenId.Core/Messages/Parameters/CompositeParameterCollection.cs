#region Copyright Preamble

// Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides an implementation of the <see cref="IParameterCollection"/> abstraction that combines multiple sources.
/// </summary>
[PublicAPI]
public class CompositeParameterCollection : IParameterCollection
{
    private IParameterCollection[] Sources { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeParameterCollection"/> class.
    /// </summary>
    public CompositeParameterCollection(params IParameterCollection[] sources)
    {
        if (sources.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(sources), "At least one source must be provided.");

        Sources = sources;
    }

    /// <inheritdoc />
    public IEnumerator<IParameter> GetEnumerator()
    {
        var visitor = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in Sources)
        {
            foreach (var parameter in source)
            {
                if (visitor.Add(parameter.Descriptor.ParameterName))
                {
                    yield return parameter;
                }
            }
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    /// <inheritdoc />
    public bool Contains(string parameterName) =>
        Sources.Any(source => source.Contains(parameterName));

    /// <inheritdoc />
    public bool TryGet(string parameterName, [MaybeNullWhen(false)] out IParameter parameter)
    {
        foreach (var source in Sources)
        {
            if (source.TryGet(parameterName, out parameter))
            {
                return true;
            }
        }

        parameter = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGet<T>(ParameterKey<T> key, [MaybeNullWhen(false)] out IParameter<T> parameter)
    {
        foreach (var source in Sources)
        {
            if (source.TryGet(key, out parameter))
            {
                return true;
            }
        }

        parameter = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGet<T>(KnownParameter<T> knownParameter, [MaybeNullWhen(false)] out IParameter<T> parameter)
    {
        foreach (var source in Sources)
        {
            if (source.TryGet(knownParameter, out parameter))
            {
                return true;
            }
        }

        parameter = null;
        return false;
    }

    /// <inheritdoc />
    public T? GetValueOrDefault<T>(ParameterKey<T> key)
    {
        foreach (var source in Sources)
        {
            if (source.TryGet(key, out var parameter))
            {
                return parameter.ParsedValue;
            }
        }

        return default;
    }

    /// <inheritdoc />
    public T? GetValueOrDefault<T>(KnownParameter<T> knownParameter)
    {
        foreach (var source in Sources)
        {
            if (source.TryGet(knownParameter, out var parameter))
            {
                return parameter.ParsedValue;
            }
        }

        return default;
    }

    /// <inheritdoc />
    public bool Remove(string parameterName) =>
        Sources.Aggregate(false, (removed, source) => removed | source.Remove(parameterName));

    /// <inheritdoc />
    public void Set(IParameter parameter) =>
        Sources[0].Set(parameter);
}
