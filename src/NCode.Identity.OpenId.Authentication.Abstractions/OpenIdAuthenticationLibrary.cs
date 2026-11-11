// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Authentication;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/>.
/// </summary>
[PublicAPI]
public readonly struct OpenIdAuthenticationLibrary
    : IRegistrationMarker<OpenIdAuthenticationLibrary>;
