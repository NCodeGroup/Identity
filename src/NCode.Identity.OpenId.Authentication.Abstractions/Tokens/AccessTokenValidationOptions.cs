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
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Contains what a reader of an access token adds to the policy <see cref="IIssuedTokenValidator"/> validates the token
/// under: the audiences the reader requires, and the keys of the reader that may decrypt the token besides the tenant's.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AccessTokenValidationOptions
{
    /// <summary>
    /// Gets the audiences the access token must name in its <c>aud</c> claim; the token is valid when it names any of
    /// them. When empty, the audience is not checked.
    /// </summary>
    public IReadOnlyCollection<string> RequiredAudiences { get; init; } = [];

    /// <summary>
    /// Gets the keys, besides the tenant's, that may decrypt an encrypted access token — the keys of a resource server
    /// that decrypts the tokens issued for it, when that resource server is the reader.
    /// </summary>
    public IReadOnlyCollection<SecretKey> AdditionalDecryptionKeys { get; init; } = [];
}
