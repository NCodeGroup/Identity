// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Contains a batch of a replica's usage and the number the replica adds it under; numbers follow the order in which
/// the replica adds its batches to the ledger.
/// </summary>
/// <param name="Number">The number of the batch, from one, in the order the replica adds its batches.</param>
/// <param name="Usage">The usage in the batch.</param>
internal readonly record struct TenantUsageBatch(long Number, PersistedTenantUsage[] Usage);
