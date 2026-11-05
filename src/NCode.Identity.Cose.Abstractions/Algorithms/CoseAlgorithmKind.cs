// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Describes the class of a COSE algorithm (RFC 9052 §8), which decides the message structure that processes it.
/// </summary>
// The classes are a closed set: RFC 9052 defines exactly these four, and each is processed by a message structure that
// this library implements, so a fifth class needs a change to the message processing as well as a new member here.
// The algorithms themselves, which are open, are CoseAlgorithm descriptors. See ADR-1409.
[PublicAPI]
public enum CoseAlgorithmKind
{
    /// <summary>
    /// A digital signature algorithm, used by COSE_Sign1 and COSE_Sign.
    /// </summary>
    Signature,

    /// <summary>
    /// A message authentication code algorithm, used by COSE_Mac0 and COSE_Mac.
    /// </summary>
    Mac,

    /// <summary>
    /// An authenticated encryption algorithm, used by COSE_Encrypt0 and COSE_Encrypt.
    /// </summary>
    ContentEncryption,

    /// <summary>
    /// A key distribution algorithm: how a recipient obtains the content key of COSE_Mac and COSE_Encrypt.
    /// </summary>
    KeyDistribution,
}
