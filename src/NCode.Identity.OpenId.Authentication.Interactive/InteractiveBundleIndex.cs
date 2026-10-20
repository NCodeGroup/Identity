#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The single-page bundle's <c>index.html</c> as last read, with the file metadata that identifies that version.
/// </summary>
/// <param name="LastWriteTimeUtc">The file's last-write time when it was read.</param>
/// <param name="Length">The file's length, in bytes, when it was read.</param>
/// <param name="Html">The file's contents.</param>
/// <param name="Shell">The contents split at the mount point, or <c>null</c> when they have none.</param>
internal sealed record InteractiveBundleIndex(
    DateTime LastWriteTimeUtc,
    long Length,
    string Html,
    InteractiveBundleShell? Shell
);
