// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;
using NCode.Identity.Secrets.RemoteKeys;
using KvSignatureAlgorithm = Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault;

/// <summary>
/// Provides an implementation of <see cref="IRemoteKeyProvider"/> that creates signing keys in Azure Key Vault or
/// Managed HSM, signs with them, and disables them, so the private half of a key never leaves the service. The
/// provider reaches the service with the credentials of the deployment.
/// </summary>
/// <remarks>
/// <para>
/// Every key the provider creates is a version of the Key Vault key that is named after its group, with the suffix
/// <c>-h</c> for a hardware-protected key and <c>-s</c> for a software-protected one, so an owner's successive keys for
/// one algorithm are the versions of one key. The identifier of a key is the identifier of its version, and a result
/// without a version is refused. Key Vault deletes whole keys only, so destroying a key disables its version.
/// </para>
/// <para>
/// A key is created for signing and verifying only, tagged with its owner, secret, algorithm and group. Key Vault signs
/// a digest, so the provider refuses a row that names no digest algorithm. The provider refuses a signature that the
/// service reports as made by another key version or with another algorithm, with a
/// <see cref="CryptographicException"/>. Logs and messages carry identifiers only, never a digest or a signature.
/// </para>
/// </remarks>
// See ADR-1471.
internal class AzureKeyVaultRemoteKeyProvider(
    IAzureKeyVaultClientFactory clientFactory,
    IOptions<AzureKeyVaultRemoteKeyProviderOptions> options
) : IRemoteKeyProvider
{
    /// <summary>
    /// The suffix of the name of a hardware-protected key.
    /// </summary>
    internal const string HardwareSuffix = "-h";

    /// <summary>
    /// The suffix of the name of a software-protected key.
    /// </summary>
    internal const string SoftwareSuffix = "-s";

    /// <summary>
    /// The name of the tag that carries the identifier of the owner of a key.
    /// </summary>
    internal const string OwnerTagKey = "ncode-identity-owner";

    /// <summary>
    /// The name of the tag that carries the identifier of the secret of a key.
    /// </summary>
    internal const string SecretTagKey = "ncode-identity-secret";

    /// <summary>
    /// The name of the tag that carries the JOSE algorithm of a key.
    /// </summary>
    internal const string AlgorithmTagKey = "ncode-identity-algorithm";

    /// <summary>
    /// The name of the tag that carries the group of a key, the name of the stable key whose versions are one owner's
    /// successive keys.
    /// </summary>
    internal const string GroupTagKey = "ncode-identity-group";

    private IAzureKeyVaultClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<AzureKeyVaultRemoteKeyProviderOptions> ProviderOptions { get; } = options;

    /// <inheritdoc />
    public string Name => RemoteKeyProviderNames.AzureKeyVault;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured vault URI is not set.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the algorithm of <paramref name="request"/> is not for an RSA or an ECC key, or an ECC key without
    /// a curve.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service creates a key whose identifier is not that of a key version, or that holds no public
    /// key.
    /// </exception>
    public async ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerOptions = ProviderOptions.Value;
        if (string.IsNullOrEmpty(providerOptions.VaultUri))
        {
            throw new InvalidOperationException(
                "SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri is not set."
            );
        }

        var vaultUri = new Uri(providerOptions.VaultUri);
        var name =
            request.GroupName
            + (providerOptions.HardwareProtected ? HardwareSuffix : SoftwareSuffix);
        var row = request.Algorithm;

        var key = row.SecretType switch
        {
            SecretKeyTypes.Rsa => await CreateRsaKeyAsync(
                vaultUri,
                CreateRsaOptions(request, name, providerOptions.HardwareProtected),
                cancellationToken
            ),
            SecretKeyTypes.Ecc => await CreateEcKeyAsync(
                vaultUri,
                CreateEcOptions(request, name, providerOptions.HardwareProtected),
                cancellationToken
            ),
            _ => throw new ArgumentException(
                $"The secret type '{row.SecretType}' is not one that Azure Key Vault holds.",
                nameof(request)
            ),
        };

        var keyId = key.Id?.AbsoluteUri;
        if (!AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            throw new CryptographicException(
                "Azure Key Vault created a key whose identifier is not that of a key version."
            );
        }

        return new RemoteKeyCreationResult
        {
            KeyId = keyId,
            SubjectPublicKeyInfo = ExportSubjectPublicKeyInfo(key, keyId),
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key identifier of <paramref name="request"/> is not that of a Key Vault key version, or the
    /// algorithm of the request names no digest algorithm.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service reports that another key version or another algorithm made the signature.
    /// </exception>
    public async ValueTask<byte[]> SignAsync(
        RemoteSignatureRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var keyId = request.KeyId;
        if (!AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not that of a Key Vault key version.",
                nameof(request)
            );
        }

        var row = request.Algorithm;
        if (row.DigestAlgorithm is null)
        {
            throw new ArgumentException(
                $"Azure Key Vault signs a digest, but the algorithm '{row.Algorithm}' names no digest algorithm.",
                nameof(request)
            );
        }

        var algorithm = new KvSignatureAlgorithm(row.SigningAlgorithm);
        var digest = request.Data.ToArray();

        var result = await ClientFactory.ExecuteAsync(
            keyId,
            access: null,
            (client, token) =>
                new ValueTask<SignResult>(client.SignAsync(algorithm, digest, token)),
            cancellationToken
        );

        if (
            result.KeyId is not { } resultKeyId
            || !AzureKeyVaultKeyId.IsVersionOf(resultKeyId, keyId)
        )
        {
            throw new CryptographicException(
                $"Azure Key Vault key '{keyId}' was asked to sign, but the service reports another key made the signature."
            );
        }

        if (result.Algorithm != algorithm)
        {
            throw new CryptographicException(
                $"Azure Key Vault key '{keyId}' was asked to sign with '{row.SigningAlgorithm}', but the service reports another algorithm made the signature."
            );
        }

        return result.Signature;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="keyId"/> is not that of a Key Vault key version.
    /// </exception>
    public async ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keyId);

        if (!AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not that of a Key Vault key version.",
                nameof(keyId)
            );
        }

        // The key lives in the vault that its identifier names, which stays right when the configured vault changes.
        var keyUri = new Uri(keyId);
        var vaultUri = new Uri(keyUri.GetLeftPart(UriPartial.Authority));
        var properties = new KeyProperties(keyUri) { Enabled = false };

        try
        {
            await ClientFactory.ExecuteKeyClientAsync(
                vaultUri,
                (client, token) =>
                    new ValueTask<Response<KeyVaultKey>>(
                        client.UpdateKeyPropertiesAsync(properties, keyOperations: null, token)
                    ),
                cancellationToken
            );
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // The key version is already deleted.
        }
    }

    private static CreateRsaKeyOptions CreateRsaOptions(
        RemoteKeyCreationRequest request,
        string name,
        bool hardwareProtected
    )
    {
        var options = new CreateRsaKeyOptions(name, hardwareProtected)
        {
            KeySize = request.Algorithm.KeySizeBits,
        };

        AddOperationsAndTags(options, request);
        return options;
    }

    private static CreateEcKeyOptions CreateEcOptions(
        RemoteKeyCreationRequest request,
        string name,
        bool hardwareProtected
    )
    {
        var curve = request.Algorithm.Curve;
        if (string.IsNullOrEmpty(curve))
        {
            throw new ArgumentException(
                $"The algorithm '{request.Algorithm.Algorithm}' of an ECC key names no curve.",
                nameof(request)
            );
        }

        var options = new CreateEcKeyOptions(name, hardwareProtected)
        {
            CurveName = new KeyCurveName(curve),
        };

        AddOperationsAndTags(options, request);
        return options;
    }

    private static void AddOperationsAndTags(
        CreateKeyOptions options,
        RemoteKeyCreationRequest request
    )
    {
        options.KeyOperations.Add(KeyOperation.Sign);
        options.KeyOperations.Add(KeyOperation.Verify);

        options.Tags[OwnerTagKey] = request.OwnerId;
        options.Tags[SecretTagKey] = request.SecretId;
        options.Tags[AlgorithmTagKey] = request.Algorithm.Algorithm;
        options.Tags[GroupTagKey] = request.GroupName;
    }

    private async ValueTask<KeyVaultKey> CreateRsaKeyAsync(
        Uri vaultUri,
        CreateRsaKeyOptions createOptions,
        CancellationToken cancellationToken
    )
    {
        var response = await ClientFactory.ExecuteKeyClientAsync(
            vaultUri,
            (client, token) =>
                new ValueTask<Response<KeyVaultKey>>(
                    client.CreateRsaKeyAsync(createOptions, token)
                ),
            cancellationToken
        );

        return response.Value;
    }

    private async ValueTask<KeyVaultKey> CreateEcKeyAsync(
        Uri vaultUri,
        CreateEcKeyOptions createOptions,
        CancellationToken cancellationToken
    )
    {
        var response = await ClientFactory.ExecuteKeyClientAsync(
            vaultUri,
            (client, token) =>
                new ValueTask<Response<KeyVaultKey>>(client.CreateEcKeyAsync(createOptions, token)),
            cancellationToken
        );

        return response.Value;
    }

    private static byte[] ExportSubjectPublicKeyInfo(KeyVaultKey key, string keyId)
    {
        if (key.Key is not { } webKey)
        {
            throw new CryptographicException($"Azure Key Vault key '{keyId}' holds no public key.");
        }

        if (webKey.KeyType == KeyType.Rsa || webKey.KeyType == KeyType.RsaHsm)
        {
            using var rsa = webKey.ToRSA();
            return rsa.ExportSubjectPublicKeyInfo();
        }

        if (webKey.KeyType == KeyType.Ec || webKey.KeyType == KeyType.EcHsm)
        {
            using var ecdsa = webKey.ToECDsa();
            return ecdsa.ExportSubjectPublicKeyInfo();
        }

        throw new CryptographicException(
            $"Azure Key Vault key '{keyId}' is not an RSA or an ECC key."
        );
    }
}
