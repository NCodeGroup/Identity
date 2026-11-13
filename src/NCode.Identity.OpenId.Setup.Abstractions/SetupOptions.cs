// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Configures the one-time instance setup. Bound from the <c>Setup</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class SetupOptions
{
    /// <summary>
    /// The name of the configuration section the options bind from.
    /// </summary>
    public const string SectionName = "Setup";

    /// <summary>
    /// Gets or sets the configured setup token, or <c>null</c> to have the host generate one. A configured token is at
    /// least <see cref="SetupDefaults.MinimumTokenLength"/> characters long.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets how long a generated setup token remains valid. Defaults to one hour.
    /// </summary>
    public TimeSpan GeneratedTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the file-system path to the built setup bundle (the <c>apps/setup</c> surface), absolute or
    /// relative to the host content root.
    /// </summary>
    public string? BundleRootPath { get; set; }

    /// <summary>
    /// Gets or sets the root administrator that the headless <c>setup</c> command creates.
    /// </summary>
    public SetupRootOptions Root { get; set; } = new();

    /// <summary>
    /// Gets or sets the management console's confidential client that the headless <c>setup</c> command registers.
    /// </summary>
    public SetupManagementConsoleOptions ManagementConsole { get; set; } = new();

    /// <summary>
    /// Gets or sets the automation client that the headless <c>setup</c> command registers.
    /// </summary>
    public SetupAutomationOptions Automation { get; set; } = new();

    /// <summary>
    /// Gets or sets what the deployment tells the setup wizard, bound from the <c>Setup:Wizard</c> section. Its
    /// <c>Setup:Wizard:ManagementConsole</c> section only prefills the wizard's console step and registers nothing.
    /// </summary>
    public SetupWizardOptions Wizard { get; set; } = new();
}
