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
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options that configure how stored secrets are protected, bound from the
/// <see cref="DefaultSectionName"/> configuration section. Each root key provider binds its own options from a child
/// of the <c>RootKeyProviders</c> section, so the options a provider needs are not part of this type.
/// </summary>
// See ADR-0235.
[PublicAPI]
public sealed class SecretProtectionOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection";

    /// <summary>
    /// Gets or sets the name of the root key provider that protects the master keys, for example
    /// <see cref="RootKeyProviderNames.Ephemeral"/>, <see cref="RootKeyProviderNames.Local"/>,
    /// <see cref="RootKeyProviderNames.AwsKms"/>, or the name of any other registered provider. When unset or blank,
    /// the ephemeral provider is used: it keeps a random key in memory, so every restart makes the stored secrets
    /// unreadable, and Production refuses it.
    /// </summary>
    public string? RootKeyProvider { get; set; }

    /// <summary>
    /// Gets or sets the options for the offline recovery key that every master key is additionally wrapped under.
    /// </summary>
    public RecoveryKeyOptions Recovery { get; set; } = new();
}
