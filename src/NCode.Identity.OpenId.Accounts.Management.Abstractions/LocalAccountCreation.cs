// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Management;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Represents the outcome of staging a local account through <see cref="ILocalAccountCreator"/>. When
/// <see cref="Succeeded"/> is <c>true</c>, the principal, its self-issued identity and the local account are staged on
/// the store manager and the caller saves the store manager to persist them. When <see cref="Error"/> is set, nothing
/// is staged.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountCreation
{
    /// <summary>
    /// Gets the identifier of the staged principal, or <c>null</c> when <see cref="Error"/> is set.
    /// </summary>
    public string? PrincipalId { get; init; }

    /// <summary>
    /// Gets the staged local account, or <c>null</c> when <see cref="Error"/> is set.
    /// </summary>
    public PersistedLocalAccount? Account { get; init; }

    /// <summary>
    /// Gets the <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when the
    /// account is staged.
    /// </summary>
    public ManagementError? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account is staged, which is when <see cref="Error"/> is <c>null</c>.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(PrincipalId))]
    [MemberNotNullWhen(true, nameof(Account))]
    public bool Succeeded => Error is null;
}
