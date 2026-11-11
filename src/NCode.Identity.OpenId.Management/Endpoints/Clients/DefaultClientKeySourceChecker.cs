// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientKeySourceChecker"/>, which reads the key type of a secret
/// from the <see cref="ISecretKeyTypeCatalog"/>.
/// </summary>
// See ADR-1161.
internal class DefaultClientKeySourceChecker(ISecretKeyTypeCatalog secretKeyTypeCatalog)
    : IClientKeySourceChecker
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public bool HasJwksUri(JsonElement settings) =>
        settings.ValueKind == JsonValueKind.Object
        && settings.TryGetPropertyValue<string>(OpenIdSettingNames.JwksUri, out var jwksUri)
        && !string.IsNullOrEmpty(jwksUri);

    /// <inheritdoc />
    public bool IsPublicKeyType(string secretType) =>
        SecretKeyTypeCatalog.TryGet(secretType, out var descriptor) && descriptor.IsAsymmetric;

    /// <inheritdoc />
    public bool HasPublicKeys(IEnumerable<PersistedSecret> secrets) =>
        secrets.Any(secret => IsPublicKeyType(secret.SecretType));
}
