// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdPlatform"/> abstraction.
/// </summary>
internal class DefaultOpenIdPlatform(
    string platformId,
    IReadOnlySettingCollectionProvider configuredSettingsProvider,
    IReadOnlySettingCollectionProvider settingsProvider,
    Func<CancellationToken, ValueTask> refreshSettingsAsync,
    IPropertyBag propertyBag
) : OpenIdPlatform, IAsyncDisposable
{
    private Func<CancellationToken, ValueTask> RefreshSettings { get; } = refreshSettingsAsync;

    /// <inheritdoc />
    public override string PlatformId { get; } = platformId;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider { get; } = settingsProvider;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider ConfiguredSettingsProvider { get; } =
        configuredSettingsProvider;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    public override ValueTask RefreshSettingsAsync(CancellationToken cancellationToken) =>
        RefreshSettings(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ConfiguredSettingsProvider.DisposeAsync();
        await SettingsProvider.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
