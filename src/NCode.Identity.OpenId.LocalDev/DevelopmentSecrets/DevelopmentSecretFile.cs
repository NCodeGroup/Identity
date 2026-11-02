// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Buffers;

namespace NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

/// <summary>
/// Generates and reads the per-machine development secrets that the AppHost hands to the hosts it starts: each is a
/// file of random bytes, base64url encoded, kept outside the repository.
/// </summary>
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
    /// is replaced. The secret only ever exists as bytes, zeroed once written — never as a string.
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

            // Move never overwrites: when another AppHost moved its secret into place first, that secret is the one to use.
            try
            {
                File.Move(temporaryPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                return path;
            }

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
    /// Reads the text of a development secret file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The text of the file.</returns>
    internal static string ReadText(string path) => File.ReadAllText(path, SecureEncoding.UTF8);

    /// <summary>
    /// Reads a development secret file and encodes its bytes as base64url.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The base64url encoding of the bytes of the file.</returns>
    internal static string ReadBase64UrlEncoded(string path) =>
        Base64Url.EncodeToString(File.ReadAllBytes(path));
}
