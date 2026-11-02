// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Contains the various types of supported cryptographic algorithms.
/// </summary>
[PublicAPI]
public enum AlgorithmType
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies that a cryptographic algorithm is used for digital signatures.
    /// </summary>
    DigitalSignature,

    /// <summary>
    /// Specifies that a cryptographic algorithm is used for key management.
    /// </summary>
    KeyManagement,

    /// <summary>
    /// Specifies that a cryptographic algorithm is used for authenticated encryption (AEAD).
    /// </summary>
    AuthenticatedEncryption,

    /// <summary>
    /// Specifies that a cryptographic algorithm is used for compressing plaintext data before encryption.
    /// </summary>
    Compression,
}
