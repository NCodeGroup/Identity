// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Bff.Server;
using NCode.Identity.Registration;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Provides a builder for configuring the management console and the backend for frontend (BFF) it is served through.
/// </summary>
[PublicAPI]
public interface IManagementConsoleBuilder : IServiceBuilder<ManagementConsole>
{
    /// <summary>
    /// Gets the builder for configuring the BFF that serves the console, such as its shared-state adapter, its secrets
    /// and its login gates.
    /// </summary>
    IIdentityBffBuilder IdentityBffBuilder { get; }
}
