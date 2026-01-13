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
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="IPersistedGrantService"/> abstraction.
/// </summary>
public class DefaultPersistedGrantService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IStoreManagerFactory storeManagerFactory
) : IPersistedGrantService
{
    // This uses C# compiler's ability to refer to static data directly.
    // For more information see https://vcsjones.dev/2019/02/01/csharp-readonly-span-bytes-static
    private static ReadOnlySpan<char> TenantDelimiter => "~~";

    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private static string GetHashInput(PersistedGrantId grantId) =>
        string.IsNullOrEmpty(grantId.TenantId) ?
            grantId.GrantKey :
            string.Concat(grantId.TenantId.AsSpan(), TenantDelimiter, grantId.GrantKey.AsSpan());

    private string GetHashedKey(PersistedGrantId grantId) =>
        CryptoService.HashValue(
            GetHashInput(grantId),
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Base64
        );

    /// <inheritdoc />
    public PersistedGrantId CreateGrantId(string? tenantId, string grantType, string grantKey) =>
        new()
        {
            TenantId = tenantId,
            GrantType = grantType,
            GrantKey = grantKey
        };

    /// <inheritdoc />
    public async ValueTask AddAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    )
    {
        if (grant.Status != PersistedGrantStatus.Active)
            throw new InvalidOperationException("The grant must be active.");

        var openIdEnvironment = openIdContext.Environment;

        var hashedKey = GetHashedKey(grantId);

        var expiresWhen = createdWhen + lifetime;

        var payloadJson = JsonSerializer.SerializeToElement(
            grant.Payload,
            openIdEnvironment.JsonSerializerOptions
        );

        var envelope = new PersistedGrant
        {
            GrantType = grantId.GrantType,
            HashedKey = hashedKey,
            TenantId = grant.TenantId,
            ClientId = grant.ClientId,
            SubjectId = grant.SubjectId,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            RevokedWhen = null,
            ConsumedWhen = null,
            PayloadJson = payloadJson
        };

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.AddAsync(envelope, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    private static PersistedGrantStatus GetStatus(DateTimeOffset utcNow, PersistedGrant envelope)
    {
        if (envelope.RevokedWhen is not null)
            return PersistedGrantStatus.Revoked;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (envelope.ExpiresWhen <= utcNow)
            return PersistedGrantStatus.Expired;

        return PersistedGrantStatus.Active;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> GetOrDefaultAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var openIdEnvironment = openIdContext.Environment;

        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope == null)
            return null;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return null;

        var payload = envelope.PayloadJson.Deserialize<TPayload>(
            openIdEnvironment.JsonSerializerOptions
        );

        if (payload is null)
            throw new InvalidOperationException("The payload could not be deserialized.");

        var status = GetStatus(utcNow, envelope);
        var persistedGrant = new PersistedGrant<TPayload>
        {
            Status = status,
            TenantId = envelope.TenantId,
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            Payload = payload
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> ConsumeOnceOrDefault<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var openIdEnvironment = openIdContext.Environment;

        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope == null)
            return null;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return null;

        var status = GetStatus(utcNow, envelope);
        var isConsumed = envelope.ConsumedWhen is not null;
        if (status != PersistedGrantStatus.Active || isConsumed)
            return null;

        var payload = envelope.PayloadJson.Deserialize<TPayload>(
            openIdEnvironment.JsonSerializerOptions
        );

        if (payload is null)
            throw new InvalidOperationException("The payload could not be deserialized.");

        envelope.ConsumedWhen = utcNow;

        await store.UpdateAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        var persistedGrant = new PersistedGrant<TPayload>
        {
            Status = status,
            TenantId = envelope.TenantId,
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            Payload = payload
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask SetRevokedOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope is null)
            return;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return;

        if (envelope.RevokedWhen is not null)
            return;

        envelope.RevokedWhen = revokedWhen;

        await store.UpdateAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateExpirationAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope == null)
            return;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return;

        envelope.ExpiresWhen = expiresWhen;

        await store.UpdateAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);
    }
}
