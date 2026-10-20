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

namespace NCode.Identity.Server.KeyRingProtection;

/// <summary>
/// Configures how the ASP.NET Core Data Protection key ring is encrypted at rest. The key ring holds the keys that
/// protect the server's own secrets (signing and encryption key material, sessions, anti-forgery tokens), so a key ring
/// stored next to the data it protects — for example in the same database — must be wrapped with a key held somewhere
/// else. <c>AddConfiguration</c> binds this section and, when a certificate is configured, protects every new key with
/// it; a Production host refuses to start while the key ring is unprotected.
/// </summary>
/// <remarks>
/// A certificate is the configuration-driven protector. A host that wraps the key ring with a cloud key management
/// service or HSM (for example <c>ProtectKeysWithAzureKeyVault</c>) or any other <c>IXmlEncryptor</c> registers it on
/// <see cref="IIdentityServerBuilder.DataProtectionBuilder"/> instead and leaves this section unset.
/// </remarks>
[PublicAPI]
public sealed class KeyRingProtectionOptions
{
    /// <summary>
    /// The configuration section (<c>DataProtection:KeyProtection</c>) bound to <see cref="KeyRingProtectionOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "DataProtection:KeyProtection";

    /// <summary>
    /// Gets or sets the certificate that encrypts every new key, or <c>null</c> to leave key protection to the host.
    /// The certificate must carry an RSA private key, which also decrypts the keys it protected.
    /// </summary>
    public KeyRingCertificateOptions? Certificate { get; set; }

    /// <summary>
    /// Gets or sets the certificates that protected older keys and are kept only to decrypt them. List the outgoing
    /// certificate here when rotating to a new <see cref="Certificate"/>. An expired key still unprotects the data it
    /// protected, so keep the certificate until no stored data depends on a key it wrapped.
    /// </summary>
    public KeyRingCertificateOptions[] PreviousCertificates { get; set; } = [];
}
