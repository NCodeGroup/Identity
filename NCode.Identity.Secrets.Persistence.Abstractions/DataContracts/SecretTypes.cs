#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Contains constants for the possible values of the <see cref="PersistedSecret.SecretType"/> property.
/// </summary>
[PublicAPI]
public static class SecretTypes
{
    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents a <c>x509 certificate</c> secret key.
    /// The underlying key material may be <c>RSA</c> or <c>ECC</c>.
    /// </summary>
    public const string Certificate = "x509";

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents a <c>symmetric</c> secret key.
    /// </summary>
    public const string Symmetric = "symmetric";

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents an <c>RSA</c> secret key without a certificate.
    /// </summary>
    public const string Rsa = "rsa";

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents an <c>Elliptic-Curve</c> secret key without a certificate.
    /// </summary>
    public const string Ecc = "ecc";
}
