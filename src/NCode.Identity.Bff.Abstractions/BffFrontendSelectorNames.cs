// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known names of frontend selection rules.
/// </summary>
/// <remarks>
/// The names are an open vocabulary: a package may register a rule under a name that is not listed here. Names are
/// compared ordinally.
/// </remarks>
[PublicAPI]
public static class BffFrontendSelectorNames
{
    /// <summary>
    /// Selects a frontend by the host, and optionally the port, of the request.
    /// </summary>
    public const string Host = "host";

    /// <summary>
    /// Selects a frontend by a local path prefix of the request.
    /// </summary>
    public const string Path = "path";
}
