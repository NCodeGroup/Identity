// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.SharedState;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the shared-state library. It is
/// minted by <c>AddSharedStateServices</c>; the shared-state adapters (<c>AddInProcessExpiringKeySet</c>,
/// <c>AddInProcessExpiringCounterSet</c>, <c>AddInProcessExpiringValueSet</c>, <c>AddInProcessExpiringIndexSet</c>,
/// <c>AddInProcessReplicaBroadcaster</c>, <c>AddValkey</c>) require its builder so they cannot be added without the shared-state library.
/// </summary>
[PublicAPI]
public readonly struct SharedStateLibrary : IRegistrationMarker<SharedStateLibrary>;
