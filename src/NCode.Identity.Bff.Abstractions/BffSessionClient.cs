// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The OAuth client that BFF sessions were issued to: the frontend, and the authority and client id the frontend was
/// configured with.
/// </summary>
/// <param name="Frontend">The name of the frontend.</param>
/// <param name="Authority">The authority that the frontend was configured with.</param>
/// <param name="ClientId">The client id that the frontend was configured with.</param>
// See ADR-1443.
[PublicAPI]
public readonly record struct BffSessionClient(string Frontend, string Authority, string ClientId);
