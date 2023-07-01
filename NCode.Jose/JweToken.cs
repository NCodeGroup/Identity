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

using System.Buffers;
using Validation;

namespace NCode.Jose;

internal class JweRecipient
{
    // header
    // unencoded JSON object

    // encrypted_key
    // BASE64URL(JWE Encrypted Key)
}

/// <summary>
/// https://www.rfc-editor.org/rfc/rfc7516
/// </summary>
internal readonly ref struct JweToken
{
    public JweType JweType { get; }

    /// <summary>
    /// JWE Protected Header
    /// </summary>
    public ReadOnlySpan<char> EncodedProtectedHeader { get; }

    /// <summary>
    /// JWE Shared Unprotected Header
    /// </summary>
    /// <remarks>
    /// Only available in JSON form.
    /// </remarks>
    public ReadOnlySpan<char> EncodedUnprotectedHeader { get; }

    /// <summary>
    /// JWE Per-Recipient Unprotected Headers
    /// </summary>
    /// <remarks>
    /// Only available in JSON form.
    /// </remarks>
    public IReadOnlyCollection<IReadOnlyDictionary<string, object>> PerRecipientUnprotectedHeaders { get; }

    /// <summary>
    /// JWE Encrypted Key
    /// </summary>
    public ReadOnlySpan<char> EncodedEncryptedKey { get; }

    /// <summary>
    /// JWE Initialization Vector
    /// </summary>
    public ReadOnlySpan<char> EncodedInitializationVector { get; }

    /// <summary>
    /// JWE Ciphertext
    /// </summary>
    public ReadOnlySpan<char> EncodedCiphertext { get; }

    /// <summary>
    /// JWE Authentication Tag
    /// </summary>
    public ReadOnlySpan<char> EncodedAuthenticationTag { get; }

    /// <summary>
    /// JWE Additional Authentication Data (AAD)
    /// </summary>
    /// <remarks>
    /// Only available in JSON form.
    /// </remarks>
    public ReadOnlySpan<char> EncodedAdditionalAuthenticationData { get; }

    public static JweToken ParseCompact(ReadOnlySequenceSegment<char> iterator)
    {
        return new JweToken(iterator);
    }

    private JweToken(ReadOnlySequenceSegment<char> iterator)
    {
        JweType = JweType.Compact;

        /*
              BASE64URL(UTF8(JWE Protected Header)) || '.' ||
              BASE64URL(JWE Encrypted Key) || '.' ||
              BASE64URL(JWE Initialization Vector) || '.' ||
              BASE64URL(JWE Ciphertext) || '.' ||
              BASE64URL(JWE Authentication Tag)
        */

        EncodedProtectedHeader = iterator.Memory.Span;

        iterator = iterator.Next ?? throw new InvalidOperationException();

        EncodedEncryptedKey = iterator.Memory.Span;

        iterator = iterator.Next ?? throw new InvalidOperationException();

        EncodedInitializationVector = iterator.Memory.Span;

        iterator = iterator.Next ?? throw new InvalidOperationException();

        EncodedCiphertext = iterator.Memory.Span;

        iterator = iterator.Next ?? throw new InvalidOperationException();

        EncodedAuthenticationTag = iterator.Memory.Span;

        Assumes.Null(iterator.Next);

        EncodedUnprotectedHeader = ReadOnlySpan<char>.Empty;
        PerRecipientUnprotectedHeaders = Array.Empty<IReadOnlyDictionary<string, object>>();
        EncodedAdditionalAuthenticationData = ReadOnlySpan<char>.Empty;
    }
}
