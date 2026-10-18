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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants for the possible values of the <see cref="PersistedSecret.SecretType"/> property — the
/// crypto family, shared with the runtime key-type taxonomy (<see cref="SecretKeyTypes"/>). Whether the stored
/// material is a certificate, a bare public key, or a private key is an orthogonal axis
/// (<see cref="SecretEncodings"/>, ADR-0104), not a distinct secret type.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretTypes
{
    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents a <c>symmetric</c> secret key.
    /// </summary>
    public const string Symmetric = SecretKeyTypes.Symmetric;

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents an <c>RSA</c> secret key.
    /// </summary>
    public const string Rsa = SecretKeyTypes.Rsa;

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents an <c>Elliptic-Curve</c> secret key.
    /// </summary>
    public const string Ecc = SecretKeyTypes.Ecc;
}
