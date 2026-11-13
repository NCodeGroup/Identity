// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Represents the outcome of staging a client through <see cref="IClientCreator"/>. When <see cref="Succeeded"/> is
/// <c>true</c>, the client is staged on the store manager and the caller saves the store manager to persist it. When
/// <see cref="Error"/> or <see cref="SettingErrors"/> is set, nothing is staged.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientCreation
{
    private ClientCreation() { }

    /// <summary>
    /// Gets the staged client, or <c>null</c> when it is not staged.
    /// </summary>
    public PersistedClient? Client { get; private init; }

    /// <summary>
    /// Gets the <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when none
    /// failed or the client's settings are invalid.
    /// </summary>
    public ManagementError? Error { get; private init; }

    /// <summary>
    /// Gets the errors of the client's settings keyed by setting name, each with its messages in order, or <c>null</c>
    /// when the settings are valid. The shape is that of the <c>errors</c> member of a validation problem.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? SettingErrors { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the client is staged.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Client))]
    public bool Succeeded => Client is not null;

    /// <summary>
    /// Creates the outcome of a client that is staged.
    /// </summary>
    /// <param name="client">The staged client.</param>
    /// <returns>The <see cref="ClientCreation"/> carrying the staged client.</returns>
    public static ClientCreation Staged(PersistedClient client) => new() { Client = client };

    /// <summary>
    /// Creates the outcome of a client that a precondition refuses.
    /// </summary>
    /// <param name="error">The <see cref="ManagementError"/> describing the failed precondition.</param>
    /// <returns>The <see cref="ClientCreation"/> carrying the error.</returns>
    public static ClientCreation Failed(ManagementError error) => new() { Error = error };

    /// <summary>
    /// Creates the outcome of a client whose settings cannot be honored.
    /// </summary>
    /// <param name="settingErrors">The errors keyed by setting name, each with its messages in order.</param>
    /// <returns>The <see cref="ClientCreation"/> carrying the setting errors.</returns>
    public static ClientCreation InvalidSettings(
        IReadOnlyDictionary<string, string[]> settingErrors
    ) => new() { SettingErrors = settingErrors };
}
