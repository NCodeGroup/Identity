using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;

namespace NCode.Cryptography.Keys;

partial struct SecretKeyReader
{
    private delegate T ImportPemDelegate<out T>(ReadOnlySpan<char> pem);

    private delegate void ImportDelegate<in T>(T key, ReadOnlySpan<byte> source, out int bytesRead)
        where T : AsymmetricAlgorithm;

    private delegate ImportDelegate<T>? GetImportDelegate<in T>(ReadOnlySpan<char> label)
        where T : AsymmetricAlgorithm;

    private unsafe T ReadPem<T>(ImportPemDelegate<T> importPem)
    {
        var charCount = Encoding.UTF8.GetCharCount(Source);
        var lease = ArrayPool<char>.Shared.Rent(charCount);
        try
        {
            // ReSharper disable once UnusedVariable
            fixed (char* pinned = lease)
            {
                try
                {
                    var pem = lease.AsSpan(0, charCount);
                    var bytesRead = Encoding.UTF8.GetChars(Source, pem);
                    Debug.Assert(bytesRead == Source.Length);

                    return importPem(pem);
                }
                finally
                {
                    Array.Clear(lease);
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(lease);
        }
    }

    private static T ImportAsymmetricKeyPem<T>(Func<T> factory, ReadOnlySpan<char> pem, GetImportDelegate<T> pemCallback)
        where T : AsymmetricAlgorithm
    {
        ImportDelegate<T>? lastAction = null;
        PemFields foundFields = default;
        ReadOnlySpan<char> foundSlice = default;

        while (PemEncoding.TryFind(pem, out var fields))
        {
            var label = pem[fields.Label];

            if (label.SequenceEqual(PemLabels.EncryptedPkcs8PrivateKey))
            {
                // not supported
                throw new InvalidOperationException();
            }

            var currentAction = pemCallback(label);
            if (currentAction != null)
            {
                if (lastAction != null)
                {
                    // duplicate keys
                    throw new InvalidOperationException();
                }

                lastAction = currentAction;
                foundFields = fields;
                foundSlice = pem;
            }

            var offset = fields.Location.End;
            pem = pem[offset..];
        }

        if (lastAction is null)
        {
            // no pem found
            throw new InvalidOperationException();
        }

        var decodedDataLength = foundFields.DecodedDataLength;
        var base64Data = foundSlice[foundFields.Base64Data];

        return ImportAsymmetricKeyPem(factory, lastAction, decodedDataLength, base64Data);
    }

    private static T ImportAsymmetricKeyPem<T>(Func<T> factory, ImportDelegate<T> importDelegate, int decodedDataLength, ReadOnlySpan<char> base64Data)
        where T : AsymmetricAlgorithm
    {
        using var lease = CryptoPool.Rent(decodedDataLength, out Span<byte> keyData);

        if (!Convert.TryFromBase64Chars(base64Data, keyData, out var bytesWritten))
        {
            throw new InvalidOperationException();
        }

        Debug.Assert(bytesWritten == keyData.Length);

        var key = factory();
        try
        {
            importDelegate(key, keyData, out var bytesRead);
            Debug.Assert(bytesRead == keyData.Length);
        }
        catch
        {
            key.Dispose();
            throw;
        }

        return key;
    }
}
