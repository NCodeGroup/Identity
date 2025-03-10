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

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Provides the signature for a delegate to validate the claims in a Json Web Token (JWT).
/// </summary>
[PublicAPI]
public delegate ValueTask ValidateJwtAsync(
    ValidateJwtContext context,
    CancellationToken cancellationToken);

/// <summary>
/// Provides the signature for a delegate that is used to return a collection of <see cref="SecretKey"/> instances
/// that are to be used to validate a Json Web Token (JWT).
/// </summary>
[PublicAPI]
public delegate ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
    CompactJwt compactJwt,
    IPropertyBag propertyBag,
    ISecretKeyCollectionProvider secretKeyCollectionProvider,
    CancellationToken cancellationToken);

/// <summary>
/// Contains the signature for a delegate that is used to create a <see cref="ClaimsIdentity"/> instance
/// from a decoded Json Web Token (JWT).
/// </summary>
[PublicAPI]
public delegate ValueTask<ClaimsIdentity> CreateClaimsIdentityAsync(
    DecodedJwt decodedJwt,
    IPropertyBag propertyBag,
    string authenticationType,
    string nameClaimType,
    string roleClaimType,
    CancellationToken cancellationToken);

/// <summary>
/// Contains a set of parameters that are used to validate a Json Web Token (JWT).
/// </summary>
[PublicAPI]
public class ValidateJwtParameters
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
    /// Gets a collection of <see cref="ValidateJwtAsync"/> delegates that are used to validate the claims in a Json Web Token (JWT).
    /// </summary>
    public ICollection<ValidateJwtAsync> Validators { get; } = new List<ValidateJwtAsync>();

    /// <summary>
    /// Gets or sets a delegate that is used to return a collection of <see cref="SecretKey"/> instances that are to be used
    /// to validate a Json Web Token (JWT).
    /// </summary>
    public ResolveValidationKeysAsync ResolveValidationKeysAsync { get; set; } =
        static (compactJwt, _, secretKeyProvider, _) =>
            ValueTask.FromResult(
                DefaultValidationKeyResolver.ResolveValidationKeys(
                    compactJwt.ProtectionType,
                    compactJwt.DeserializedHeader,
                    secretKeyProvider.Collection));

    /// <summary>
    /// Gets or sets a delegate that is used to create a <see cref="ClaimsIdentity"/> instance from a Json Web Token (JWT).
    /// </summary>
    public CreateClaimsIdentityAsync CreateClaimsIdentityAsync { get; set; } =
        static (decodedJwt, _, authenticationType, nameClaimType, roleClaimType, _) =>
            ValueTask.FromResult(
                DefaultClaimsIdentityFactory.CreateClaimsIdentity(
                    authenticationType,
                    nameClaimType,
                    roleClaimType,
                    decodedJwt.Payload));

    /// <summary>
    /// Gets or sets the amount of time to allow for clock skew when validating <see cref="DateTime"/> claims.
    /// The default is <c>300</c> seconds (5 minutes).
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5.0);
}
