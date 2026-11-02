// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.Jose;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.PropertyBag;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides the signature for a delegate that returns a collection of <see cref="SecretKey"/> instances
/// that are to be used to validate a JSON Web Token (JWT).
/// </summary>
[PublicAPI]
public delegate ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
    JoseToken token,
    IPropertyBag propertyBag,
    ISecretKeyCollectionProvider secretKeyCollectionProvider,
    CancellationToken cancellationToken
);

/// <summary>
/// Provides the signature for a delegate that creates a <see cref="ClaimsIdentity"/> instance
/// from a decoded JSON Web Token (JWT).
/// </summary>
[PublicAPI]
public delegate ValueTask<ClaimsIdentity> CreateClaimsIdentityAsync(
    DecodedJwt decodedJwt,
    IPropertyBag propertyBag,
    string authenticationType,
    string nameClaimType,
    string roleClaimType,
    CancellationToken cancellationToken
);
