#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Text.Json;
using IdGen;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="IPersistedGrantService"/> abstraction.
/// </summary>
public class DefaultPersistedGrantService : IPersistedGrantService
{
    private ISystemClock SystemClock { get; }
    private ICryptoService CryptoService { get; }
    private IIdGenerator<long> IdGenerator { get; }
    private IPersistedGrantStore PersistedGrantStore { get; }
    private OpenIdServer OpenIdServer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPersistedGrantService"/> class.
    /// </summary>
    public DefaultPersistedGrantService(
        ISystemClock systemClock,
        ICryptoService cryptoService,
        IIdGenerator<long> idGenerator,
        IPersistedGrantStore persistedGrantStore,
        OpenIdServer openIdServer)
    {
        SystemClock = systemClock;
        CryptoService = cryptoService;
        IdGenerator = idGenerator;
        PersistedGrantStore = persistedGrantStore;
        OpenIdServer = openIdServer;
    }

    private string GetHashedKey(string grantKey) =>
        CryptoService.HashValue(
            grantKey,
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64);

    /// <inheritdoc />
    public async ValueTask AddAsync<TPayload>(
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var id = IdGenerator.CreateId();
        var hashedKey = GetHashedKey(grantId.GrantKey);
        var createdWhen = SystemClock.UtcNow;
        var expiresWhen = createdWhen.Add(lifetime);

        var payloadJson = JsonSerializer.Serialize(
            grant.Payload,
            OpenIdServer.JsonSerializerOptions);

        var envelope = new PersistedGrant
        {
            Id = id,
            TenantId = grantId.TenantId,
            GrantType = grantId.GrantType,
            HashedKey = hashedKey,
            ClientId = grant.ClientId,
            SubjectId = grant.SubjectId,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            PayloadJson = payloadJson
        };

        await PersistedGrantStore.AddAsync(envelope, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> TryGetAsync<TPayload>(
        PersistedGrantId grantId,
        bool singleUse,
        bool setConsumed,
        CancellationToken cancellationToken)
    {
        var utcNow = SystemClock.UtcNow;
        var hashedKey = GetHashedKey(grantId.GrantKey);

        var envelope = await PersistedGrantStore.TryGetAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            cancellationToken);

        if (envelope == null)
            return default;

        if (envelope.ExpiresWhen <= utcNow)
            return default;

        var isConsumed = envelope.ConsumedWhen.HasValue;

        if (singleUse && isConsumed)
            return default;

        if (setConsumed && !isConsumed)
        {
            await PersistedGrantStore.SetConsumedOnceAsync(
                grantId.TenantId,
                grantId.GrantType,
                hashedKey,
                utcNow,
                cancellationToken);
        }

        var payload = JsonSerializer.Deserialize<TPayload>(
            envelope.PayloadJson,
            OpenIdServer.JsonSerializerOptions);

        if (payload is null)
            return default;

        var persistedGrant = new PersistedGrant<TPayload>
        {
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            Payload = payload
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask SetConsumedAsync(
        PersistedGrantId grantId,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken)
    {
        var hashedKey = GetHashedKey(grantId.GrantKey);

        await PersistedGrantStore.SetConsumedOnceAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            consumedWhen,
            cancellationToken);
    }
}
