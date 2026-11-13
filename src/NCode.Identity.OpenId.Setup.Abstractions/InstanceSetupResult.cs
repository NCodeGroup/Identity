// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Setup.Contracts;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Represents the outcome of creating the root administrator through <see cref="IInstanceSetupService"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class InstanceSetupResult
{
    /// <summary>
    /// Gets the created root administrator, or <c>null</c> when <see cref="Error"/> is set.
    /// </summary>
    public CreateRootResponse? Root { get; init; }

    /// <summary>
    /// Gets the <c>client_id</c> of the management console's client that setup registered, or <c>null</c> when setup
    /// registered none.
    /// </summary>
    public string? ManagementConsoleClientId { get; init; }

    /// <summary>
    /// Gets the <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when the root
    /// administrator is created.
    /// </summary>
    public ManagementError? Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the root administrator is created, which is when <see cref="Error"/> is
    /// <c>null</c>.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(Root))]
    public bool Succeeded => Error is null;
}
