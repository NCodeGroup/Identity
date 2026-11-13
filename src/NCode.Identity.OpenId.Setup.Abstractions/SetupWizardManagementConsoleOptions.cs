// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Names the management console for the setup wizard's console step. Bound from the
/// <c>Setup:Wizard:ManagementConsole</c> configuration section. It only prefills the wizard's console step and registers
/// nothing, unlike <see cref="SetupManagementConsoleOptions"/>. Its origin is the one the setup page may fetch the
/// console's public JWK Set from.
/// </summary>
[PublicAPI]
public sealed class SetupWizardManagementConsoleOptions
{
    /// <summary>
    /// Gets or sets the management console's origin, an absolute <c>https</c> URL without a path, query or fragment.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the <c>client_id</c> that the wizard prefills for the management console. Defaults to
    /// <see cref="SetupManagementConsoleOptions.DefaultClientId"/>.
    /// </summary>
    public string ClientId { get; set; } = SetupManagementConsoleOptions.DefaultClientId;

    /// <summary>
    /// Gets a value indicating whether the management console is named, which is when <see cref="BaseUrl"/> is set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(BaseUrl))]
    public bool IsConfigured => BaseUrl is not null;
}
