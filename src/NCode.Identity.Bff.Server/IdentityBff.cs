// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;

namespace NCode.Identity.Bff.Server;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/>.
/// </summary>
[PublicAPI]
public readonly struct IdentityBff : IRegistrationMarker<IdentityBff>;
