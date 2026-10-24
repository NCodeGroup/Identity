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

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Options;

/// <summary>
/// Contains the options for the Google Cloud Key Management Service root key provider, bound from the
/// <see cref="DefaultSectionName"/> configuration section.
/// </summary>
// See ADR-0238.
[PublicAPI]
public sealed class GoogleCloudKmsRootKeyOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "SecretProtection:RootKeyProviders:GoogleCloudKms";

    /// <summary>
    /// Gets or sets the resource name of the symmetric <c>ENCRYPT_DECRYPT</c> Cloud KMS crypto key that wraps master
    /// keys, without a version
    /// (<c>projects/&lt;project&gt;/locations/&lt;location&gt;/keyRings/&lt;key ring&gt;/cryptoKeys/&lt;key&gt;</c>).
    /// The service encrypts with the crypto key's primary version.
    /// </summary>
    public string? KeyName { get; set; }
}
