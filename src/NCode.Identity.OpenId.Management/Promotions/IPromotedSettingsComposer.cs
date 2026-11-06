// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Composes the settings document a promotion writes to a target owner from the source owner's document, the target
/// owner's own document, and the environment-bound values the request supplies.
/// </summary>
// See ADR-1425.
internal interface IPromotedSettingsComposer
{
    /// <summary>
    /// Composes a settings document that holds the source's settings that are not environment-bound, then the
    /// target's environment-bound settings, then the supplied environment-bound settings.
    /// </summary>
    /// <param name="sourceSettings">The source owner's settings document.</param>
    /// <param name="targetSettings">The target owner's settings document, or <c>null</c> when it has none.</param>
    /// <param name="environmentSettings">The supplied environment-bound settings, or <c>null</c> when there are none.</param>
    /// <returns>The <see cref="PromotedSettings"/> that holds the composed document.</returns>
    PromotedSettings Compose(
        JsonElement sourceSettings,
        JsonElement? targetSettings,
        JsonElement? environmentSettings
    );
}
