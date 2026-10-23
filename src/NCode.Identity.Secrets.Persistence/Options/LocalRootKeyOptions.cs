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

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options for the root key provider that reads the root key from a local file or from the
/// configuration, bound from the <see cref="DefaultSectionName"/> configuration section. Exactly one of
/// <see cref="KeyPath"/> and <see cref="Key"/> must be set.
/// </summary>
// See ADR-0228.
[PublicAPI]
public sealed class LocalRootKeyOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection:RootKeyProviders:Local";

    /// <summary>
    /// Gets or sets the path of the file that holds the current root key as a base64url-encoded 32-byte key.
    /// </summary>
    public string? KeyPath { get; set; }

    /// <summary>
    /// Gets or sets the current root key inline as a base64url-encoded 32-byte key. This is intended for Development
    /// and tests only; Production refuses it.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the paths of the files that hold older root keys, kept so that the master keys they wrapped can
    /// still be unwrapped.
    /// </summary>
    public string[] PreviousKeyPaths { get; set; } = [];
}
