// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the outcome of a rewrap of the persisted master keys.
/// </summary>
/// <param name="Examined">The number of master keys that were read.</param>
/// <param name="Rewrapped">The number of master keys that were written.</param>
/// <param name="Verified">The number of master keys that a dry run unwrapped and verified.</param>
/// <param name="Failed">The number of master keys that could not be rewrapped.</param>
// See ADR-1408.
[PublicAPI]
public sealed record MasterKeyRewrapResult(int Examined, int Rewrapped, int Verified, int Failed);
