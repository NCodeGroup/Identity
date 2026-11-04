// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants for the known lifecycle states of a <see cref="PersistedSecret"/>, judged at an instant from its
/// <see cref="PersistedSecret.ActivatesWhen"/>, <see cref="PersistedSecret.RetiresWhen"/> and
/// <see cref="PersistedSecret.ExpiresWhen"/>. The states are names, compared ordinally.
/// </summary>
// See ADR-1403.
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretLifecycleStates
{
    /// <summary>
    /// The secret is published, and verifies or decrypts, ahead of its active window: it does not sign or encrypt yet.
    /// </summary>
    public const string Upcoming = "upcoming";

    /// <summary>
    /// The secret is in its active window: it signs or encrypts, and it verifies or decrypts.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// The secret's active window has closed and its validity window has not: it verifies or decrypts what it signed
    /// or what was encrypted to it, and is still published, but signs or encrypts nothing more.
    /// </summary>
    public const string Retired = "retired";

    /// <summary>
    /// The secret's validity window has closed: it neither verifies nor decrypts, is not published, and no longer
    /// authenticates.
    /// </summary>
    public const string Expired = "expired";
}
