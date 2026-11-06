// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.ManagementConsole.Host;

/// <summary>
/// A public marker type that identifies this assembly as the application under test. Integration tests
/// use it as the <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> entry point so the real host is discovered
/// and booted through the full HTTP pipeline.
/// </summary>
[PublicAPI]
public sealed class ManagementConsoleHostMarker;
