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
    string displayName,
    bool isDisabled,
    AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> settingsReference,
    IPropertyBag propertyBag
) : OpenIdTeam
{
    private AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> SettingsReference { get; set; } =
        settingsReference.AddReference();

    /// <inheritdoc />
    public override string TeamId { get; } = teamId;

    /// <inheritdoc />
    public override string DisplayName { get; } = displayName;

    /// <inheritdoc />
    public override bool IsDisabled { get; } = isDisabled;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider => SettingsReference.Value;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await SettingsReference.DisposeAsync();
        SettingsReference = default;
    }
}
