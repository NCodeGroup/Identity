// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The single-page bundle's <c>index.html</c> as last read, with the file metadata that identifies that version.
/// </summary>
/// <param name="LastWriteTimeUtc">The file's last-write time when it was read.</param>
/// <param name="Length">The file's length, in bytes, when it was read.</param>
/// <param name="Shell">The file's contents split at the mount point, or <c>null</c> when they have none.</param>
internal sealed record InteractiveBundleIndex(
    DateTime LastWriteTimeUtc,
    long Length,
    InteractiveBundleShell? Shell
);
