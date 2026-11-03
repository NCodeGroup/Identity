// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants for the possible values of the <see cref="PersistedSecret.Origin"/> property: where a secret's
/// material came from. The origin tells how much entropy the material is known to have, which decides how a shared
/// secret's one-way verifier is made: a generated secret's entropy is known, a supplied secret's is not.
/// </summary>
// See ADR-1391.
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretOrigins
{
    /// <summary>
    /// Indicates that the server generated the secret's material from its own random source, as
    /// <see cref="ISecretGenerator"/> does.
    /// </summary>
    public const string Generated = "generated";

    /// <summary>
    /// Indicates that a caller supplied the secret's material, such as a configured <c>client_secret</c> or an
    /// imported key, so its entropy is not known.
    /// </summary>
    public const string Supplied = "supplied";
}
