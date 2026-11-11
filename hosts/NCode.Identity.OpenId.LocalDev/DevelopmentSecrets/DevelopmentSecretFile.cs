// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Buffers;

namespace NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

/// <summary>
/// Generates and reads the per-machine development secrets that the AppHost hands to the hosts it starts: each is a
/// file, kept outside the repository, whose text is the base64url encoding of 32 random bytes. Each caller chooses how
/// the file becomes its secret: <see cref="ReadText"/> uses the text itself, and <see cref="ReadBase64UrlEncoded"/>
/// encodes the bytes of that text once more.
/// </summary>
/// <remarks>
/// The secret is generated and read as bytes in buffers that are zeroed once used. The readers still return a
/// <see cref="string"/>, because the AppHost hands each secret to the hosts it starts as an environment variable, and
/// that value can only be a string.
/// </remarks>
// See ADR-1235 and ADR-1370.
internal static class DevelopmentSecretFile
{
    /// <summary>
    /// Gets the directory beside which every development secret of this machine is kept.
    /// </summary>
    /// <returns>The <c>NCode.Identity/LocalDev</c> directory under the local application data of the user.</returns>
    internal static string GetDefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create
            ),
            "NCode.Identity",
            "LocalDev"
        );

    /// <summary>
    /// Makes sure the development secret file exists, generating it on the first run.
    /// </summary>
    /// <remarks>
    /// Every OP instance that shares the dev database must unwrap the same master keys, and the secret stays out of the
    /// repository: the first AppHost run on a machine writes a random 32-byte secret, base64url encoded, to
    /// <paramref name="directory"/> and every later run reuses it. The secret is written to a temporary file and moved
    /// into place, so a crash never leaves a partial secret under the final name; an empty file left by an older run
    /// is replaced. While it is generated, the secret exists only as bytes on the stack, zeroed once written.
    /// </remarks>
    /// <param name="directory">The directory that holds the file, created when missing.</param>
    /// <param name="fileName">The name of the file within <paramref name="directory"/>.</param>
    /// <returns>The path of the file.</returns>
    internal static string Ensure(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        if (File.Exists(path))
        {
            // An empty file can only be left by an interrupted in-place write of an older run.
            if (new FileInfo(path).Length == 0)
                File.Delete(path);
            else
                return path;
        }

        var temporaryPath = Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");

        Span<byte> key = stackalloc byte[32];
        Span<byte> text = stackalloc byte[43];
        try
        {
            RandomNumberGenerator.Fill(key);

            var status = Base64Url.EncodeToUtf8(key, text, out _, out var written);
            if (status != OperationStatus.Done)
                throw new InvalidOperationException(
                    $"Encoding the development secret failed with status {status}."
                );

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
                stream.Write(text[..written]);
                stream.Flush(flushToDisk: true);
            }

            MoveIntoPlace(temporaryPath, path);

            return path;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(text);

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
    /// Moves a newly written secret file into place, unless another AppHost has already moved its own there.
    /// </summary>
    /// <remarks>
    /// The move never overwrites: when another AppHost moved its secret into place first, that secret is the one every
    /// run uses, and the newly written file is deleted.
    /// </remarks>
    /// <param name="temporaryPath">The path of the newly written secret file.</param>
    /// <param name="path">The final path of the secret file.</param>
    internal static void MoveIntoPlace(string temporaryPath, string path)
    {
        try
        {
            File.Move(temporaryPath, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Reads the text of a development secret file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The text of the file.</returns>
    internal static string ReadText(string path) =>
        Read(path, static bytes => SecureEncoding.UTF8.GetString(bytes));

    /// <summary>
    /// Reads a development secret file and encodes its bytes as base64url.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The base64url encoding of the bytes of the file.</returns>
    internal static string ReadBase64UrlEncoded(string path) =>
        Read(path, static bytes => Base64Url.EncodeToString(bytes));

    private static string Read(string path, Func<ReadOnlySpan<byte>, string> encode)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);

        var length = checked((int)RandomAccess.GetLength(handle));
        using var lease = BufferFactory.Rent(length, isSensitive: true, out Span<byte> bytes);

        var bytesRead = RandomAccess.Read(handle, bytes, 0);

        return encode(bytes[..bytesRead]);
    }
}
