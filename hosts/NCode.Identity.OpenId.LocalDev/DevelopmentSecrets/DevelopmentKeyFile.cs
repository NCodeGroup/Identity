// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;

namespace NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

/// <summary>
/// Generates and reads the per-machine development private keys that the AppHost hands to the hosts it starts: each is
/// a PEM file, kept outside the repository, that holds one P-256 private key as PKCS#8.
/// </summary>
/// <remarks>
/// A client that authenticates with <c>private_key_jwt</c> signs with such a key. The OP registers the client's public
/// key by value, through <see cref="ReadPublicJwks"/>, because its outbound guard never fetches a <c>jwks_uri</c> on a
/// loopback address, which is where every development host listens.
/// </remarks>
internal static class DevelopmentKeyFile
{
    // The most characters that the PKCS#8 PEM of a P-256 private key takes, with a margin.
    private const int MaxPemLength = 512;

    /// <summary>
    /// Makes sure the development key file exists, generating a P-256 private key on the first run.
    /// </summary>
    /// <remarks>
    /// The key stays out of the repository: the first AppHost run on a machine writes a new key, as a PKCS#8 PEM, to
    /// <paramref name="directory"/> and every later run reuses it. The key is written to a temporary file and moved
    /// into place, so a crash never leaves a partial key under the final name; an empty file left by an interrupted
    /// run is replaced. While it is generated, the PEM exists only in a sensitive buffer, zeroed once written.
    /// </remarks>
    /// <param name="directory">The directory that holds the file, created when missing.</param>
    /// <param name="fileName">The name of the file within <paramref name="directory"/>.</param>
    /// <returns>The path of the file.</returns>
    internal static string EnsureEcPrivateKey(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        if (File.Exists(path))
        {
            // An empty file is the remains of an interrupted write, so it is replaced.
            if (new FileInfo(path).Length == 0)
                File.Delete(path);
            else
                return path;
        }

        var temporaryPath = Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            using var charsLease = BufferFactory.Rent(
                MaxPemLength,
                isSensitive: true,
                out Span<char> chars
            );
            using var bytesLease = BufferFactory.Rent(
                MaxPemLength,
                isSensitive: true,
                out Span<byte> bytes
            );

            using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                if (!key.TryExportPkcs8PrivateKeyPem(chars, out var charsWritten))
                    throw new InvalidOperationException(
                        "The development key does not fit the buffer that holds its PEM."
                    );

                chars = chars[..charsWritten];
            }

            var bytesWritten = SecureEncoding.ASCII.GetBytes(chars, bytes);

            // The temporary name is unique, so CreateNew only guards against a stray file of the same name.
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            using (var stream = new FileStream(temporaryPath, options))
            {
                stream.Write(bytes[..bytesWritten]);
                stream.Flush(flushToDisk: true);
            }

            DevelopmentSecretFile.MoveIntoPlace(temporaryPath, path);

            return path;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Cleanup is best-effort and must not hide the real outcome.
            }
        }
    }

    /// <summary>
    /// Reads the public half of a development P-256 private key as a JSON Web Key Set.
    /// </summary>
    /// <remarks>
    /// The OP registers a development client's public key by value because its outbound guard never fetches a
    /// <c>jwks_uri</c> on a loopback address.
    /// </remarks>
    /// <param name="path">The path of the PEM file.</param>
    /// <returns>
    /// The JSON text of a set with one <c>ES256</c> signing key, whose <c>kid</c> is the name of the file without its
    /// extension.
    /// </returns>
    internal static string ReadPublicJwks(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);

        var length = checked((int)RandomAccess.GetLength(handle));
        using var bytesLease = BufferFactory.Rent(length, isSensitive: true, out Span<byte> buffer);
        var bytesRead = RandomAccess.Read(handle, buffer, 0);
        var bytes = buffer[..bytesRead];

        var charCount = SecureEncoding.UTF8.GetCharCount(bytes);
        using var charsLease = BufferFactory.Rent(
            charCount,
            isSensitive: true,
            out Span<char> chars
        );
        var charsWritten = SecureEncoding.UTF8.GetChars(bytes, chars);

        using var key = ECDsa.Create();
        key.ImportFromPem(chars[..charsWritten]);
        var parameters = key.ExportParameters(includePrivateParameters: false);

        var writer = BufferFactory.CreateArrayBufferWriter();
        using (var json = new Utf8JsonWriter(writer))
        {
            json.WriteStartObject();
            json.WriteStartArray("keys");
            json.WriteStartObject();
            json.WriteString("kty", "EC");
            json.WriteString("crv", "P-256");
            json.WriteString("x", Base64Url.EncodeToString(parameters.Q.X));
            json.WriteString("y", Base64Url.EncodeToString(parameters.Q.Y));
            json.WriteString("kid", Path.GetFileNameWithoutExtension(path));
            json.WriteString("use", "sig");
            json.WriteString("alg", "ES256");
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        return SecureEncoding.UTF8.GetString(writer.WrittenSpan);
    }
}
