#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Auditing;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The default implementation of <see cref="IBffSessionManager"/>.
/// </summary>
internal class DefaultBffSessionManager(IBffSessionStore store, IBffAuditRecorder recorder)
    : IBffSessionManager
{
    private const int CreateAttempts = 2;

    private IBffSessionStore Store { get; } = store;

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
    public ValueTask<BffSessionState?> UpdateAsync(
        BffSessionState current,
        Func<BffSessionState, BffSessionState?> update,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask EndAsync(
        HttpContext? httpContext,
        string storeKey,
        BffSessionMetadata metadata,
        string reason,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
