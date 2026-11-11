// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.Claims;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the claims library. It is minted
/// by <c>AddClaimsLibrary</c>; the libraries built on claims require its builder.
/// </summary>
[PublicAPI]
public readonly struct ClaimsLibrary : IRegistrationMarker<ClaimsLibrary>;
