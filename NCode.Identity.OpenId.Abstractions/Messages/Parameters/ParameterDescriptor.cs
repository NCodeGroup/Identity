#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Contains information about how an <c>OAuth</c> or <c>OpenID Connect</c> parameter should be parsed and loaded.
/// </summary>
public readonly struct ParameterDescriptor : IEquatable<ParameterDescriptor>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterDescriptor"/> struct using a <see cref="KnownParameter"/>.
    /// </summary>
    /// <param name="knownParameter">The <see cref="KnownParameter"/>.</param>
    public ParameterDescriptor(KnownParameter knownParameter)
    {
        ParameterName = knownParameter.Name;
        KnownParameter = knownParameter;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterDescriptor"/> struct using a parameter name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    public ParameterDescriptor(string parameterName)
    {
        ParameterName = parameterName;
        KnownParameter = null;
    }

    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> or <c>null</c> if none provided.
    /// </summary>
    public KnownParameter? KnownParameter { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter allows missing string values when parsing. Returns the result from
    /// <see cref="KnownParameter"/> if one was provided; otherwise always returns <c>true</c>.
    /// </summary>
    public bool AllowMissingStringValues => KnownParameter?.AllowMissingStringValues ?? true;

    /// <summary>
    /// Gets a value indicating whether the parameter should sort string values when serializing. Returns the result from
    /// <see cref="KnownParameter"/> if one was provided; otherwise always returns <c>false</c>.
    /// </summary>
    public bool SortStringValues => KnownParameter?.SortStringValues ?? false;

    /// <summary>
    /// Gets a value indicating whether the parameter allows multiple string values when parsing. Returns the result from
    /// <see cref="KnownParameter"/> if one was provided; otherwise always returns <c>false</c>.
    /// </summary>
    public bool AllowMultipleStringValues => KnownParameter?.AllowMultipleStringValues ?? false;

    /// <summary>
    /// Gets the <see cref="ParameterLoader"/> that can be used to parse and return a <see cref="Parameter"/> given
    /// <see cref="StringValues"/>. Uses the result from <see cref="KnownParameter"/> if one was provided; otherwise
    /// always returns <see cref="ParameterLoader.Default"/>.
    /// </summary>
    public ParameterLoader Loader => KnownParameter?.Loader ?? ParameterLoader.Default;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ParameterDescriptor other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(ParameterDescriptor other) =>
        KnownParameter == null ?
            string.Equals(ParameterName, other.ParameterName, StringComparison.Ordinal) :
            KnownParameter == other.KnownParameter;

    /// <inheritdoc/>
    public override int GetHashCode() =>
        KnownParameter?.GetHashCode() ?? StringComparer.Ordinal.GetHashCode(ParameterName);

    /// <summary/>
    public static bool operator ==(ParameterDescriptor left, ParameterDescriptor right) =>
        left.Equals(right);

    /// <summary/>
    public static bool operator !=(ParameterDescriptor left, ParameterDescriptor right) =>
        !(left == right);
}
