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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Contains the options used to configure how an authenticated caller is resolved to a stable principal identifier.
/// </summary>
[PublicAPI]
public sealed class PrincipalResolutionOptions
{
    /// <summary>
    /// Contains the default name of the claim that carries the subject value (the OpenID <c>sub</c>).
    /// </summary>
    public const string DefaultSourceClaimName = "sub";

    /// <summary>
    /// Contains the default name of the claim that carries the upstream issuer (the OpenID <c>iss</c>).
    /// </summary>
    public const string DefaultIssuerClaimName = "iss";

    /// <summary>
    /// Gets or sets the name of the claim that carries the subject value used to resolve the principal. The subject is
    /// interpreted first as a server-owned <c>PrincipalId</c> and, when that does not match, as an upstream subject
    /// paired with <see cref="IssuerClaimName"/>. The default value is <see cref="DefaultSourceClaimName"/>.
    /// </summary>
    public string SourceClaimName { get; set; } = DefaultSourceClaimName;

    /// <summary>
    /// Gets or sets the name of the claim that carries the upstream issuer used, together with the subject, to resolve
    /// an external connection identity. The default value is <see cref="DefaultIssuerClaimName"/>.
    /// </summary>
    public string IssuerClaimName { get; set; } = DefaultIssuerClaimName;
}
