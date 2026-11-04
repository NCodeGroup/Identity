// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdServer"/> abstraction.
/// </summary>
internal class DefaultOpenIdServer(
    string serverId,
    IReadOnlySettingCollectionProvider configuredSettingsProvider,
    IReadOnlySettingCollectionProvider settingsProvider,
    ISecretKeyCollectionProvider secretsProvider,
    Func<CancellationToken, ValueTask> refreshSettingsAsync,
    IPropertyBag propertyBag
) : OpenIdServer, IAsyncDisposable
{
    private Func<CancellationToken, ValueTask> RefreshSettings { get; } = refreshSettingsAsync;

    /// <inheritdoc />
    public override string ServerId { get; } = serverId;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider { get; } = settingsProvider;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider ConfiguredSettingsProvider { get; } =
        configuredSettingsProvider;

    /// <inheritdoc />
    public override ISecretKeyCollectionProvider SecretsProvider { get; } = secretsProvider;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    public override ValueTask RefreshSettingsAsync(CancellationToken cancellationToken) =>
        RefreshSettings(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await SecretsProvider.DisposeAsync();
        await ConfiguredSettingsProvider.DisposeAsync();
        await SettingsProvider.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
