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

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.PropertyBag;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Contains the data-only set of parameters that are used to validate a JSON Web Token (JWT). Built-in validators are
/// expressed declaratively as <see cref="IJwtValidationRequirement"/> instances in <see cref="Requirements"/> (added
/// via the fluent extension methods) and are executed by registered <see cref="IJwtValidationHandler"/> strategies;
/// this type carries no validation logic.
/// </summary>
/// <remarks>
/// <para>
/// A JWT is accepted only when it is signed: a compact JWS, or a compact JWE whose <c>cty</c> header is <c>JWT</c> and
/// whose content is a compact JWS (a Nested JWT, <c>RFC 7519</c> §5.2). An unsecured JWS (<c>alg</c> <c>none</c>) and
/// an encrypted JWT that encloses no signature are always rejected; no parameter relaxes this.
/// </para>
/// <para>
/// The algorithm lists (<see cref="ValidSignatureAlgorithms"/>, <see cref="ValidKeyManagementAlgorithms"/>,
/// <see cref="ValidContentEncryptionAlgorithms"/>, and <see cref="ValidCompressionAlgorithms"/>) are checked against the
/// token's headers before any cryptographic operation is performed (<c>RFC 8725</c> §3.1). A <c>null</c> list does not
/// restrict its algorithm; a list that is set permits only the algorithms it names, so an empty list permits none.
/// </para>
/// </remarks>
[PublicAPI]
public class JwtValidationParameters
{
    /// <summary>
    /// Gets an <see cref="IPropertyBag"/> that can provide additional user-defined information about the current operation.
    /// This instance will be cloned for each JWT operation.
    /// </summary>
    public IPropertyBag PropertyBag { get; } = PropertyBagFactory.Create();

    /// <summary>
    /// Gets or sets the <c>AuthenticationType</c> that is used when creating <see cref="ClaimsIdentity"/> instances.
    /// The default value is <c>AuthenticationTypes.Federation</c>.
    /// </summary>
    public string AuthenticationType { get; set; } = "AuthenticationTypes.Federation";

    /// <summary>
    /// Gets or sets a <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Name</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.
    /// </summary>
    public string NameClaimType { get; set; } = ClaimsIdentity.DefaultNameClaimType;

    /// <summary>
    /// Gets or sets a <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Role</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.
    /// </summary>
    public string RoleClaimType { get; set; } = ClaimsIdentity.DefaultRoleClaimType;

    /// <summary>
    /// Gets or sets the amount of time to allow for clock skew when validating <see cref="DateTime"/> claims.
    /// The default is <c>300</c> seconds (5 minutes). Lifetime and certificate requirements capture this value when added.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets an explicit collection of <see cref="SecretKey"/> instances to use for validation. When set (and
    /// when <see cref="ResolveValidationKeysAsync"/> is <c>null</c>), these keys are used instead of the default
    /// <c>kid</c>/thumbprint resolution over the ambient secret-key collection.
    /// </summary>
    public IReadOnlyCollection<SecretKey>? ValidationKeys { get; set; }

    /// <summary>
    /// Gets or sets the collection of <see cref="SecretKey"/> instances that decrypt the enclosing JWE of a Nested JWT —
    /// the recipient's keys, for example the server's key that a client encrypted its request object to. The keys that
    /// verify the enclosed signature are the signer's (<see cref="ValidationKeys"/>). When <c>null</c>, an encrypted
    /// token is rejected: decryption keys are never inferred from the keys that verify signatures.
    /// </summary>
    public IReadOnlyCollection<SecretKey>? DecryptionKeys { get; set; }

    /// <summary>
    /// Gets or sets the signature (<c>alg</c>) algorithms the token may be signed with. When <c>null</c>, any
    /// registered signature algorithm other than <c>none</c> is accepted; when set, only the algorithms it names.
    /// </summary>
    public IReadOnlyCollection<string>? ValidSignatureAlgorithms { get; set; }

    /// <summary>
    /// Gets or sets the key management (<c>alg</c>) algorithms the enclosing JWE of a Nested JWT may use. When
    /// <c>null</c>, any registered key management algorithm is accepted; when set, only the algorithms it names.
    /// </summary>
    public IReadOnlyCollection<string>? ValidKeyManagementAlgorithms { get; set; }

    /// <summary>
    /// Gets or sets the content encryption (<c>enc</c>) algorithms the enclosing JWE of a Nested JWT may use. When
    /// <c>null</c>, any registered content encryption algorithm is accepted; when set, only the algorithms it names.
    /// </summary>
    public IReadOnlyCollection<string>? ValidContentEncryptionAlgorithms { get; set; }

    /// <summary>
    /// Gets or sets the compression (<c>zip</c>) algorithms the enclosing JWE of a Nested JWT may use. When
    /// <c>null</c>, any registered compression algorithm is accepted; when set, only the algorithms it names. A JWE
    /// without a <c>zip</c> header is uncompressed and always accepted.
    /// </summary>
    public IReadOnlyCollection<string>? ValidCompressionAlgorithms { get; set; }

    /// <summary>
    /// Gets the collection of <see cref="IJwtValidationRequirement"/> instances that the JSON Web Token (JWT) must satisfy.
    /// </summary>
    public List<IJwtValidationRequirement> Requirements { get; } = [];

    /// <summary>
    /// Gets or sets a delegate that resolves the <see cref="SecretKey"/> instances used to validate the token. When
    /// <c>null</c>, the implementation uses <see cref="ValidationKeys"/> if set, otherwise the default resolver.
    /// </summary>
    public ResolveValidationKeysAsync? ResolveValidationKeysAsync { get; set; }

    /// <summary>
    /// Gets or sets a delegate that creates a <see cref="ClaimsIdentity"/> instance from a decoded JSON Web Token (JWT).
    /// When <c>null</c>, the implementation uses its default claims-identity factory.
    /// </summary>
    public CreateClaimsIdentityAsync? CreateClaimsIdentityAsync { get; set; }
}
