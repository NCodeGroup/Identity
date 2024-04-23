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
public class DefaultPersistedGrantService(
    ISystemClock systemClock,
    ICryptoService cryptoService,
    IIdGenerator<long> idGenerator,
    IStoreManagerFactory storeManagerFactory,
    OpenIdServer openIdServer
) : IPersistedGrantService
{
    private ISystemClock SystemClock { get; } = systemClock;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IIdGenerator<long> IdGenerator { get; } = idGenerator;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private OpenIdServer OpenIdServer { get; } = openIdServer;

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

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var store = storeManager.GetStore<IPersistedGrantStore>();

        await store.AddAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);
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

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var store = storeManager.GetStore<IPersistedGrantStore>();

        var envelope = await store.TryGetAsync(
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

        var isDirty = false;
        if (setConsumed && !isConsumed)
        {
            await store.SetConsumedOnceAsync(
                grantId.TenantId,
                grantId.GrantType,
                hashedKey,
                utcNow,
                cancellationToken);

            isDirty = true;
        }

        var payload = JsonSerializer.Deserialize<TPayload>(
            envelope.PayloadJson,
            OpenIdServer.JsonSerializerOptions);

        if (isDirty)
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }

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

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);

        var store = storeManager.GetStore<IPersistedGrantStore>();

        await store.SetConsumedOnceAsync(
            grantId.TenantId,
            grantId.GrantType,
            hashedKey,
            consumedWhen,
            cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);
    }
}
