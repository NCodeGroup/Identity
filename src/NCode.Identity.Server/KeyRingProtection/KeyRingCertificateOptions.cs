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
/// Locates a certificate, with its RSA private key, that protects the Data Protection key ring. Two file formats are
/// accepted: a PEM certificate with a separate PEM private key (set <see cref="PrivateKeyPath"/>), as a Kubernetes TLS
/// secret mounts them, or a PKCS#12 bundle that holds both (leave <see cref="PrivateKeyPath"/> unset).
/// </summary>
[PublicAPI]
public sealed class KeyRingCertificateOptions
{
    /// <summary>
    /// Gets or sets the path of the PEM certificate file, or of the PKCS#12 (<c>.pfx</c>/<c>.p12</c>) file when
    /// <see cref="PrivateKeyPath"/> is unset.
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path of the PEM private key file that belongs to the certificate, or <c>null</c> when
    /// <see cref="CertificatePath"/> is a PKCS#12 file.
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the password of the PKCS#12 file or of the encrypted PEM private key, or <c>null</c> when it has
    /// none. Supply it from a secret store or an environment variable, never from a checked-in settings file.
    /// </summary>
    public string? Password { get; set; }
}
