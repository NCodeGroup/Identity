// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The console bundle's <c>index.html</c> as last read, with the file metadata that identifies that version.
/// </summary>
/// <param name="LastWriteTimeUtc">The file's last-write time when it was read.</param>
/// <param name="Length">The file's length, in bytes, when it was read.</param>
/// <param name="Html">The file's text.</param>
internal sealed record ManagementConsoleBundleIndex(
    DateTime LastWriteTimeUtc,
    long Length,
    string Html
);
