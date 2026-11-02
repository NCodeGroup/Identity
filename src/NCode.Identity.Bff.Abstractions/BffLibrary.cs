// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the backend-for-frontend (BFF)
/// library. It is minted by <c>AddBffLibrary</c>; extensions that add BFF strategies require its builder so they
/// cannot be added without the BFF library.
/// </summary>
[PublicAPI]
public readonly struct BffLibrary : IRegistrationMarker<BffLibrary>;
