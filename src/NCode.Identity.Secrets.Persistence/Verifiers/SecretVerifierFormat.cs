// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Makes and reads the self-describing text of a stored <see cref="SecretVerifier"/>, <c>$&lt;scheme&gt;$…</c>, as
/// ASCII octets that are protected and stored like key material. The text names its scheme, and the registered
/// <see cref="ISecretVerifierScheme"/> of that name writes and reads the parameters that follow, so a verifier keeps
/// verifying when the scheme or the parameters chosen for new secrets change.
/// </summary>
// See ADR-1391.
internal static class SecretVerifierFormat
{
    /// <summary>
    /// Gets the octet that starts the text and ends the scheme's name.
    /// </summary>
    public const byte Separator = (byte)'$';

    /// <summary>
    /// Determines whether the specified name can name a scheme: a non-empty run of printable ASCII characters other
    /// than <c>$</c>.
    /// </summary>
    /// <param name="name">The name of the scheme.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> can name a scheme.</returns>
    public static bool IsValidSchemeName(string name) =>
        !string.IsNullOrEmpty(name) && name.All(value => value is > ' ' and < '\x7f' and not '$');

    /// <summary>
    /// Writes the verifier text of a secret, <c>$&lt;scheme&gt;$&lt;parameters&gt;</c>, made with the specified scheme.
    /// </summary>
    /// <param name="scheme">The scheme that makes the verifier.</param>
    /// <param name="secret">The octets of the secret.</param>
    /// <param name="destination">The sensitive <see cref="IBufferWriter{T}"/> that receives the text.</param>
    public static void Write(
        ISecretVerifierScheme scheme,
        ReadOnlySpan<byte> secret,
        IBufferWriter<byte> destination
    )
    {
        var name = scheme.Name;
        if (!IsValidSchemeName(name))
        {
            throw new InvalidOperationException(
                $"The secret verifier scheme name '{name}' is not a run of printable ASCII characters other than '$'."
            );
        }

        var prefixLength = name.Length + 2;
        var prefix = destination.GetSpan(prefixLength);
        prefix[0] = Separator;
        SecureEncoding.ASCII.GetBytes(name, prefix.Slice(1, name.Length));
        prefix[name.Length + 1] = Separator;
        destination.Advance(prefixLength);

        scheme.WriteParameters(secret, destination);
    }

    /// <summary>
    /// Reads the text of a stored verifier with the registered scheme it names.
    /// </summary>
    /// <param name="catalog">The catalog of the registered schemes.</param>
    /// <param name="metadata">The metadata of the secret the verifier confirms.</param>
    /// <param name="text">The verifier text.</param>
    /// <param name="verifier">When this method returns <see langword="true"/>, the verifier; otherwise,
    /// <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a verifier of a registered scheme with valid
    /// parameters; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(
        ISecretVerifierSchemeCatalog catalog,
        KeyMetadata metadata,
        ReadOnlySpan<byte> text,
        [NotNullWhen(true)] out SecretVerifier? verifier
    )
    {
        verifier = null;

        if (text.IsEmpty || text[0] != Separator)
        {
            return false;
        }

        var rest = text[1..];
        var schemeEnd = rest.IndexOf(Separator);
        if (schemeEnd <= 0)
        {
            return false;
        }

        var schemeName = rest[..schemeEnd];
        if (schemeName.ContainsAnyExceptInRange((byte)'!', (byte)'~'))
        {
            return false;
        }

        return catalog.TryGet(SecureEncoding.ASCII.GetString(schemeName), out var scheme)
            && scheme.TryReadParameters(metadata, rest[(schemeEnd + 1)..], out verifier);
    }

    /// <summary>
    /// Determines whether the specified text is a non-empty run of the ASCII digits <c>0</c> to <c>9</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> holds only digits.</returns>
    public static bool IsDigits(ReadOnlySpan<byte> text) =>
        !text.IsEmpty && !text.ContainsAnyExceptInRange((byte)'0', (byte)'9');

    /// <summary>
    /// Decodes a non-empty base64url parameter that holds no <c>$</c>.
    /// </summary>
    /// <param name="text">The parameter.</param>
    /// <param name="bytes">When this method returns <see langword="true"/>, the decoded octets.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is valid base64url.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> text, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;

        if (text.IsEmpty || text.Contains(Separator) || !Base64Url.IsValid(text))
        {
            return false;
        }

        var decoded = new byte[Base64Url.GetMaxDecodedLength(text.Length)];
        if (!Base64Url.TryDecodeFromUtf8(text, decoded, out var written))
        {
            return false;
        }

        bytes = written == decoded.Length ? decoded : decoded[..written];
        return true;
    }

    /// <summary>
    /// Writes the base64url encoding of the specified octets.
    /// </summary>
    /// <param name="value">The octets.</param>
    /// <param name="destination">The <see cref="IBufferWriter{T}"/> that receives the encoding.</param>
    public static void WriteBase64Url(ReadOnlySpan<byte> value, IBufferWriter<byte> destination)
    {
        var length = Base64Url.GetEncodedLength(value.Length);
        var written = Base64Url.EncodeToUtf8(value, destination.GetSpan(length));
        destination.Advance(written);
    }

    /// <summary>
    /// Writes the separator <c>$</c>.
    /// </summary>
    /// <param name="destination">The <see cref="IBufferWriter{T}"/> that receives the separator.</param>
    public static void WriteSeparator(IBufferWriter<byte> destination)
    {
        destination.GetSpan(1)[0] = Separator;
        destination.Advance(1);
    }
}
