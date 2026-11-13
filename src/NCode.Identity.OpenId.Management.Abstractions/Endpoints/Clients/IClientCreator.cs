// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Validates a client creation request and stages the client on a store manager. The management create endpoints and
/// every other caller that registers a client share this one implementation, so each runs the same
/// <see cref="IClientValidator"/> preconditions, settings validation, coherence validation and client-authentication
/// method naming. Register a custom implementation to replace or extend the default behavior.
/// </summary>
[PublicAPI]
public interface IClientCreator
{
    /// <summary>
    /// Validates the request and stages the client on the store manager without saving. The caller saves the store
    /// manager to persist the staged client, assigns its owner and records its audit event. Nothing is staged when
    /// <see cref="ClientCreation.Error"/> or <see cref="ClientCreation.SettingErrors"/> is set.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="request">The <see cref="ClientCreationRequest"/> describing the client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ClientCreation"/> carrying the staged client, or the error or setting errors describing the first failed precondition.</returns>
    ValueTask<ClientCreation> StageAsync(
        IStoreManager storeManager,
        ClientCreationRequest request,
        CancellationToken cancellationToken
    );
}
