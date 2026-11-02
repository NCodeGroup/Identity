// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Auditing;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The default implementation of <see cref="IBffSessionManager"/>.
/// </summary>
internal class DefaultBffSessionManager(
    BffInstrumentedSessionStore store,
    IBffAuditRecorder recorder
) : IBffSessionManager
{
    private const int CreateAttempts = 2;
    private const int UpdateAttempts = 5;

    private BffInstrumentedSessionStore Store { get; } = store;

    private IBffAuditRecorder Recorder { get; } = recorder;

    /// <inheritdoc />
    public async ValueTask<BffSessionState> CreateAsync(
        BffSessionMetadata metadata,
        BffSessionPayload payload,
        CancellationToken cancellationToken
    )
    {
        const long initialVersion = 1;

        for (var attempt = 0; attempt < CreateAttempts; ++attempt)
        {
            var handle = BffKeyDerivation.CreateSecret();
            var record = BffSessionCodec.Encode(handle, initialVersion, metadata, payload);

            var created = await Store.TryCreateAsync(record, cancellationToken);
            if (created)
                return new BffSessionState(
                    handle,
                    record.StoreKey,
                    initialVersion,
                    metadata,
                    payload
                );
        }

        throw new InvalidOperationException("A unique session handle could not be created.");
    }

    /// <inheritdoc />
    public async ValueTask<BffSessionState?> LoadAsync(
        string handle,
        CancellationToken cancellationToken
    )
    {
        if (!BffSessionCodec.TryGetStoreKey(handle, out var storeKey))
            return null;

        var record = await Store.GetAsync(storeKey, cancellationToken);
        if (record is null)
            return null;

        if (!BffSessionCodec.TryDecode(handle, record, out var payload))
            return null;

        return new BffSessionState(
            handle,
            record.StoreKey,
            record.Version,
            record.Metadata,
            payload
        );
    }

    /// <inheritdoc />
    public async ValueTask<BffSessionState?> UpdateAsync(
        BffSessionState current,
        Func<BffSessionState, BffSessionState?> update,
        CancellationToken cancellationToken
    )
    {
        var state = current;

        for (var attempt = 0; attempt < UpdateAttempts; ++attempt)
        {
            var next = update(state);
            if (next is null)
                return state;

            var nextVersion = state.Version + 1;
            var record = BffSessionCodec.Encode(
                next.Handle,
                nextVersion,
                next.Metadata,
                next.Payload
            );

            var replaced = await Store.TryReplaceAsync(record, state.Version, cancellationToken);
            if (replaced)
                return next with { Version = nextVersion };

            if (attempt == UpdateAttempts - 1)
                break;

            var reloaded = await LoadAsync(current.Handle, cancellationToken);
            if (reloaded is null)
                return null;

            state = reloaded;
        }

        throw new BffStoreUnavailableException("The session changed too often to be updated.");
    }

    /// <inheritdoc />
    public async ValueTask EndAsync(
        HttpContext? httpContext,
        string storeKey,
        BffSessionMetadata metadata,
        string reason,
        CancellationToken cancellationToken
    )
    {
        await Store.RemoveAsync(storeKey, metadata, cancellationToken);

        await Recorder.SessionEndedAsync(httpContext, metadata.Subject, reason, cancellationToken);
    }
}
