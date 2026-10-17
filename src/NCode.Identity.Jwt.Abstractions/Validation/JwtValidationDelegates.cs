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
