// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Json;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// Resolves the holder key from the <c>jwk</c> member of a <c>cnf</c> claim (RFC 7800 §3.2) through the registered
/// secret key types, so every asymmetric key type the JOSE stack supports binds a presentation. A key that carries
/// private material, a symmetric key, or a key of an unregistered type resolves nothing.
/// </summary>
/// <param name="secretKeyTypeCatalog">The registered secret key types.</param>
internal class JwkConfirmationKeyResolver(ISecretKeyTypeCatalog secretKeyTypeCatalog)
    : IConfirmationKeyResolver
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public string Member => JoseClaimNames.Header.Jwk;

    /// <inheritdoc />
    public ValueTask<SecretKey?> ResolveAsync(
        JsonElement confirmation,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (
            confirmation.ValueKind != JsonValueKind.Object
            || !confirmation.TryGetPropertyValue<JsonElement>(Member, out var jwkElement)
            || jwkElement.ValueKind != JsonValueKind.Object
        )
            return ValueTask.FromResult<SecretKey?>(null);

        JsonWebKey jwk;
        try
        {
            jwk = new JsonWebKey(jwkElement);
        }
        catch (ArgumentException)
        {
            return ValueTask.FromResult<SecretKey?>(null);
        }

        if (
            !SecretKeyTypeCatalog.TryGetByJwkKeyType(jwk.KeyType, out var descriptor)
            || !descriptor.IsAsymmetric
            || descriptor.JwkKeyType is not { } jwkKeyType
            || !descriptor.PublicKeyImportFormats.Contains(SecretKeyImportFormats.Jwk)
        )
            return ValueTask.FromResult<SecretKey?>(null);

        // The key type's own private members count too, not only the standard private names: a holder key that
        // discloses its private half binds nothing.
        jwk = new JsonWebKey(jwkElement, jwkKeyType);
        if (jwk.ContainsPrivateKeyMaterial)
            return ValueTask.FromResult<SecretKey?>(null);

        // The import hands the public key bytes to a callback and reports their encoding only once it returns, so
        // the bytes are held in a rented buffer until the key is deserialized.
        IDisposable? lease = null;
        try
        {
            Memory<byte> keyMaterial = default;
            var imported = descriptor.ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = jwkElement.GetRawText(),
                },
                bytes =>
                {
                    lease?.Dispose();
                    lease = BufferFactory.Rent(bytes.Length, isSensitive: false, out keyMaterial);
                    bytes.CopyTo(keyMaterial.Span);
                    return string.Empty;
                }
            );
            var secretKey = descriptor.DeserializeKeyMaterial(
                new KeyMetadata { KeyId = jwk.KeyId },
                imported.SecretEncoding,
                keyMaterial.Span
            );
            return ValueTask.FromResult<SecretKey?>(secretKey);
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or FormatException
                        or JsonException
                        or NotSupportedException
                        or CryptographicException
            )
        {
            return ValueTask.FromResult<SecretKey?>(null);
        }
        finally
        {
            lease?.Dispose();
        }
    }
}
