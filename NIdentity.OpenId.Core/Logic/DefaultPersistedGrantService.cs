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

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPersistedGrantService"/> class.
    /// </summary>
    public DefaultPersistedGrantService(
        ISystemClock systemClock,
        ICryptoService cryptoService,
        IIdGenerator<long> idGenerator,
        IPersistedGrantStore persistedGrantStore)
    {
        SystemClock = systemClock;
        CryptoService = cryptoService;
        IdGenerator = idGenerator;
        PersistedGrantStore = persistedGrantStore;
    }

    private string GetHashedKey(string grantKey) =>
        CryptoService.HashValue(
            grantKey,
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64);

    /// <inheritdoc />
    public async ValueTask AddAsync<TPayload>(
        string tenantId,
        string grantType,
        string grantKey,
        string? clientId,
        string? subjectId,
        TimeSpan lifetime,
        TPayload payload,
        JsonSerializerOptions jsonSerializerOptions,
        CancellationToken cancellationToken)
    {
        var id = IdGenerator.CreateId();
        var hashedKey = GetHashedKey(grantKey);
        var createdWhen = SystemClock.UtcNow;
        var expiresWhen = createdWhen.Add(lifetime);
        var payloadJson = JsonSerializer.Serialize(
            payload,
            jsonSerializerOptions);

        var persistedGrant = new PersistedGrant
        {
            Id = id,
            TenantId = tenantId,
            GrantType = grantType,
            HashedKey = hashedKey,
            ClientId = clientId,
            SubjectId = subjectId,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            PayloadJson = payloadJson
        };

        await PersistedGrantStore.AddAsync(persistedGrant, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<TPayload?> TryGetAsync<TPayload>(
        string tenantId,
        string grantType,
        string grantKey,
        bool singleUse,
        bool setConsumed,
        JsonSerializerOptions jsonSerializerOptions,
        CancellationToken cancellationToken)
    {
        var utcNow = SystemClock.UtcNow;
        var hashedKey = GetHashedKey(grantKey);

        var persistedGrant = await PersistedGrantStore.TryGetAsync(
            tenantId,
            grantType,
            hashedKey,
            cancellationToken);

        if (persistedGrant == null)
            return default;

        if (persistedGrant.ExpiresWhen <= utcNow)
            return default;

        var isConsumed = persistedGrant.ConsumedWhen.HasValue;

        if (singleUse && isConsumed)
            return default;

        if (setConsumed && !isConsumed)
        {
            await PersistedGrantStore.SetConsumedOnceAsync(
                tenantId,
                grantType,
                hashedKey,
                utcNow,
                cancellationToken);
        }

        var payload = JsonSerializer.Deserialize<TPayload>(
            persistedGrant.PayloadJson,
            jsonSerializerOptions);

        return payload;
    }

    /// <inheritdoc />
    public async ValueTask SetConsumedAsync(
        string tenantId,
        string grantType,
        string grantKey,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken)
    {
        var hashedKey = GetHashedKey(grantKey);

        await PersistedGrantStore.SetConsumedOnceAsync(
            tenantId,
            grantType,
            hashedKey,
            consumedWhen,
            cancellationToken);
    }
}
