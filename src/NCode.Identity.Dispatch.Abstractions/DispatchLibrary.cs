// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.Dispatch;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the background job-dispatch library.
/// It is minted by <c>AddDispatchServices</c>; the dispatch opt-ins (<c>AddDispatchJobHandler</c>) require its builder
/// so they cannot be added without the dispatch library.
/// </summary>
[PublicAPI]
public readonly struct DispatchLibrary : IRegistrationMarker<DispatchLibrary>;
