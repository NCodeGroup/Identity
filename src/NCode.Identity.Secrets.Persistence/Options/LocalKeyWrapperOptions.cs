// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options for the local key wrapper that reads the root key from a local file or from the
/// configuration, bound from the <see cref="DefaultSectionName"/> configuration section. Exactly one of
/// <see cref="KeyPath"/> and <see cref="Key"/> must be set.
/// </summary>
// See ADR-1235.
[PublicAPI]
public sealed class LocalKeyWrapperOptions
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
    /// Gets or sets the current root key inline as a base64url-encoded 32-byte key. This is intended for local
    /// development and tests only; a host without <c>LocalDevelopment:Enabled</c> refuses it.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the paths of the files that hold older root keys, kept so that the master keys they wrapped can
    /// still be unwrapped.
    /// </summary>
    public string[] PreviousKeyPaths { get; set; } = [];
}
