// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the cryptography library. It is
/// minted by <c>AddCryptographyLibrary</c>; the libraries built on cryptography require its builder.
/// </summary>
[PublicAPI]
public readonly struct CryptographyLibrary : IRegistrationMarker<CryptographyLibrary>;
