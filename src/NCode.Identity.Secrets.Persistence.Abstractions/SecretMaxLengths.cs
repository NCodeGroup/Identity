// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants for the maximum lengths of various fields.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretMaxLengths
{
    /// <summary>
    /// Specifies the maximum length of the <c>SecretUse</c> field.
    /// </summary>
    public const int Use = 100;

    /// <summary>
    /// Specifies the maximum length of the <c>SecretAlgorithm</c> field.
    /// </summary>
    public const int Algorithm = 100;

    /// <summary>
    /// Specifies the maximum length of the <c>SecretType</c> field.
    /// </summary>
    public const int SecretType = 100;

    /// <summary>
    /// Specifies the maximum length of the <c>SecretEncoding</c> field.
    /// </summary>
    public const int SecretEncoding = 100;

    /// <summary>
    /// Specifies the maximum length of the <c>KeyId</c> field (a key's <c>kid</c>).
    /// </summary>
    public const int KeyId = 300;

    /// <summary>
    /// Specifies the maximum length of the <c>EncodedValue</c> field.
    /// </summary>
    public const int EncodedValue = 8000;

    /// <summary>
    /// Specifies the maximum length of the <c>PublicKey</c> field.
    /// </summary>
    public const int PublicKey = 4000;
}
