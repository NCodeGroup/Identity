// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Bff.Options;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Bff.Secrets;

/// <summary>
/// Loads a configured <c>Bff:KeyRings:{name}</c> entry as a static key ring: each PEM file holds the EC or RSA private
/// key of one key, whose id is the file name without the extension.
/// </summary>
// See ADR-1439.
internal static class BffPemKeyRing
{
    /// <summary>
    /// Reads every PEM file of a configured key ring and holds the keys in a static key-ring provider.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> that supplies the options, the host environment
    /// and the secret-key factories.</param>
    /// <param name="name">The name of the key ring within <c>Bff:KeyRings</c>.</param>
    /// <returns>The <see cref="ISecretKeyCollectionProvider"/> that holds the keys of the key ring.</returns>
    /// <exception cref="InvalidOperationException">A file of the key ring holds no PEM-encoded EC or RSA private key.</exception>
    public static ISecretKeyCollectionProvider Load(IServiceProvider serviceProvider, string name)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var options = serviceProvider.GetRequiredService<IOptions<BffOptions>>().Value;
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
        var keyFactory = serviceProvider.GetRequiredService<ISecretKeyFactory>();
        var collectionFactory =
            serviceProvider.GetRequiredService<ISecretKeyCollectionProviderFactory>();

        var keys = new List<SecretKey>();
        foreach (var configuredPath in options.KeyRings[name].PemFiles)
        {
            var path = Path.GetFullPath(configuredPath, environment.ContentRootPath);
            keys.Add(ReadKey(keyFactory, name, path));
        }

        return collectionFactory.CreateStatic(keys);
    }

    private static SecretKey ReadKey(ISecretKeyFactory keyFactory, string name, string path)
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
        var pem = chars[..charsWritten];

        var metadata = new KeyMetadata { KeyId = Path.GetFileNameWithoutExtension(path) };

        try
        {
            return keyFactory.CreateEccPem(metadata, pem);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // The file holds no EC private key; it may hold an RSA one.
        }

        try
        {
            return keyFactory.CreateRsaPem(metadata, pem);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The key ring '{name}' cannot read '{path}': it holds no PEM-encoded EC or RSA private key.",
                ex
            );
        }
    }
}
