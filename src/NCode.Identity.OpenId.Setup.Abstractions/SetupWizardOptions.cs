// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Describes what the deployment tells the setup wizard. Bound from the <c>Setup:Wizard</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class SetupWizardOptions
{
    /// <summary>
    /// Gets or sets the management console that the wizard's console step prefills. Bound from the
    /// <c>Setup:Wizard:ManagementConsole</c> configuration section. It registers nothing.
    /// </summary>
    public SetupWizardManagementConsoleOptions ManagementConsole { get; set; } = new();
}
