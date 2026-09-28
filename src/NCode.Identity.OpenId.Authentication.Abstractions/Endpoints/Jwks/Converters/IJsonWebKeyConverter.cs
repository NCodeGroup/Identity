#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Provides the ability to convert a <see cref="SecretKey"/> into a <see cref="JsonWebKey"/> for
/// publication by the <c>JSON Web Key Set (JWKS)</c> endpoint.
/// </summary>
/// <remarks>
/// The JWKS endpoint resolves all registered <see cref="IJsonWebKeyConverter"/> instances and asks
/// each in turn to convert a given <see cref="SecretKey"/>. To support an additional key type, register
/// a new <see cref="IJsonWebKeyConverter"/> implementation (for example by deriving from
/// <see cref="JsonWebKeyConverter{TSecretKey}"/>); no changes to the endpoint handler are required.
/// </remarks>
[PublicAPI]
public interface IJsonWebKeyConverter
{
    /// <summary>
    /// Attempts to convert the specified <paramref name="secretKey"/> into a <see cref="JsonWebKey"/>.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to convert.</param>
    /// <param name="jsonWebKey">When this method returns <c>true</c>, contains the converted
    /// <see cref="JsonWebKey"/>; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if this converter handled <paramref name="secretKey"/> and produced a
    /// <see cref="JsonWebKey"/>; otherwise, <c>false</c>.</returns>
    bool TryConvert(SecretKey secretKey, [NotNullWhen(true)] out JsonWebKey? jsonWebKey);
}
