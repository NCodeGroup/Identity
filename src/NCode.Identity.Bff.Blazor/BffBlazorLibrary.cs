// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the backend-for-frontend (BFF)
/// Blazor server library.
/// </summary>
[PublicAPI]
public readonly struct BffBlazorLibrary : IRegistrationMarker<BffBlazorLibrary>;
