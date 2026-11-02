// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the OpenID protocol client library.
/// It is minted by <c>AddOpenIdClientLibrary</c>; extensions that add client-authentication strategies require its
/// builder so they cannot be added without the client library.
/// </summary>
[PublicAPI]
public readonly struct OpenIdClientLibrary : IRegistrationMarker<OpenIdClientLibrary>;
