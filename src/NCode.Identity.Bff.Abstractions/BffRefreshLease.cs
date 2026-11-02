// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// A refresh lease that an <see cref="IBffSessionStore"/> granted: the key it guards and the random owner token that
/// only its holder knows, so a release removes only this hold.
/// </summary>
/// <param name="LeaseKey">The key of the lease.</param>
/// <param name="Owner">The owner token the store recorded when it granted the lease.</param>
// See ADR-1308.
[PublicAPI]
public readonly record struct BffRefreshLease(string LeaseKey, string Owner);
