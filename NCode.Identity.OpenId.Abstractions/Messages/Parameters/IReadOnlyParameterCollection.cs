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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a readonly collection of <see cref="IParameter"/> instances that can be accessed by their name or <see cref="ParameterKey{T}"/>.
/// </summary>
[PublicAPI]
public interface IReadOnlyParameterCollection : IEnumerable<IParameter>
{
    /// <summary>
    /// Determines whether the collection contains a parameter with the specified name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter to locate.</param>
    /// <returns><c>true</c> if the collection contains a parameter with the specified name; otherwise, <c>false</c>.</returns>
    bool Contains(string parameterName);

    /// <summary>
    /// Attempts to get an <see cref="IParameter"/> that has the specified name.
    /// </summary>
    /// <param name="parameterName">The name of the <see cref="IParameter"/> to get.</param>
    /// <param name="parameter">When this method returns, the <see cref="IParameter"/> with the specified name, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the <see cref="IParameter"/> with the specified name was found; otherwise, <c>false</c>.</returns>
    bool TryGet(string parameterName, [MaybeNullWhen(false)] out IParameter parameter);

    /// <summary>
    /// Attempts to get an <see cref="IParameter{T}"/> given its <see cref="ParameterKey{T}"/>.
    /// </summary>
    /// <param name="key">The key of the typed parameter to get.</param>
    /// <param name="parameter">When this method returns, the <see cref="IParameter{T}"/> with the specified <paramref name="key"/>,
    /// if found; otherwise, <c>null</c>.</param>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <returns><c>true</c> if the <see cref="IParameter{T}"/> with the specified <paramref name="key"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGet<T>(ParameterKey<T> key, [MaybeNullWhen(false)] out IParameter<T> parameter);

    /// <summary>
    /// Attempts to get an <see cref="IParameter{T}"/> given its <see cref="KnownParameter{T}"/>.
    /// </summary>
    /// <param name="knownParameter">The typed known parameter to get.</param>
    /// <param name="parameter">When this method returns, the <see cref="IParameter{T}"/> with the specified <paramref name="knownParameter"/>,
    /// if found; otherwise, <c>null</c>.</param>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <returns><c>true</c> if the <see cref="IParameter{T}"/> with the specified <see cref="knownParameter"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGet<T>(KnownParameter<T> knownParameter, [MaybeNullWhen(false)] out IParameter<T> parameter);

    /// <summary>
    /// Gets the value of a parameter given its <see cref="ParameterKey{T}"/>, if found; otherwise, returns the default value of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="key">The key of the typed parameter to get.</param>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <returns>The value of the parameter, if found; otherwise, the default value of <typeparamref name="T"/>.</returns>
    T? GetValueOrDefault<T>(ParameterKey<T> key);

    /// <summary>
    /// Gets the value of a parameter given its <see cref="KnownParameter{T}"/>, if found; otherwise, returns the default value of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="knownParameter">The typed known parameter to get.</param>
    /// <typeparam name="T">The type of the parameter value.</typeparam>
    /// <returns>The value of the parameter, if found; otherwise, the default value of <typeparamref name="T"/>.</returns>
    T? GetValueOrDefault<T>(KnownParameter<T> knownParameter);
}
