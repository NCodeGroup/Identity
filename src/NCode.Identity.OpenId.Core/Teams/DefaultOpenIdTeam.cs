// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Disposables;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdTeam"/> abstraction.
/// </summary>
// See ADR-1411
internal class DefaultOpenIdTeam(
    string teamId,
    string concurrencyToken,
    string displayName,
    bool isDisabled,
    string path,
    AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> settingsReference,
    AsyncSharedReferenceLease<OpenIdTeam> parentReference,
    IPropertyBag propertyBag
) : OpenIdTeam
{
    private AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> SettingsReference { get; set; } =
        settingsReference.AddReference();

    private AsyncSharedReferenceLease<OpenIdTeam> ParentReference { get; set; } = parentReference;

    /// <inheritdoc />
    public override string TeamId { get; } = teamId;

    /// <inheritdoc />
    public override string ConcurrencyToken { get; } = concurrencyToken;

    /// <inheritdoc />
    public override string DisplayName { get; } = displayName;

    /// <inheritdoc />
    public override bool IsDisabled { get; } = isDisabled;

    /// <inheritdoc />
    public override OpenIdTeam? Parent => ParentReference.IsActive ? ParentReference.Value : null;

    /// <inheritdoc />
    public override string Path { get; } = path;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider => SettingsReference.Value;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await SettingsReference.DisposeAsync();
        SettingsReference = default;

        if (ParentReference.IsActive)
            await ParentReference.DisposeAsync();

        ParentReference = default;
    }
}
