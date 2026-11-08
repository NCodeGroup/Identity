// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;
using Google.Api.Gax.Grpc;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.GoogleCloudKms;

/// <summary>
/// Provides an implementation of <see cref="IRemoteKeyProvider"/> that creates signing keys in Google Cloud Key
/// Management Service, signs with them, and destroys them, so the private half of a key never leaves the service. The
/// provider reaches the service with the credentials of the deployment.
/// </summary>
/// <remarks>
/// <para>
/// The group of a creation request names a Cloud KMS crypto key in the configured key ring, and every key the provider
/// creates for the group is a version of that crypto key. The provider creates the crypto key, a signing key with
/// the algorithm of the requested row, the configured protection level and labels that name its group and algorithm,
/// the first time a version is created in a crypto key that does not exist. The identifier of a key is the resource name
/// of its crypto key version.
/// </para>
/// <para>
/// A version of a hardware key is generated asynchronously, so the provider polls the version until it is enabled,
/// with a delay that doubles from 250 milliseconds to 4 seconds, for 60 seconds at most.
/// </para>
/// <para>
/// Every request and response carries CRC32C checksums, so a value corrupted in transit is detected. A response that
/// fails its check fails with an <see cref="IOException"/>, because the call can be retried. Reads and signing are
/// retried by the client library with jittered back-off, and so are creation and destruction when the service answers
/// <c>ResourceExhausted</c>. Logs and messages carry identifiers only, never the signing input, a digest or a
/// signature.
/// </para>
/// </remarks>
// See ADR-1471.
internal class GoogleCloudKmsRemoteKeyProvider(
    IGoogleCloudKmsClientFactory clientFactory,
    IOptions<GoogleCloudKmsRemoteKeyProviderOptions> options,
    TimeProvider timeProvider
) : IRemoteKeyProvider
{
    /// <summary>
    /// The key of the label that carries the group of a crypto key.
    /// </summary>
    internal const string GroupLabelKey = "ncode-identity-group";

    /// <summary>
    /// The key of the label that carries the JOSE algorithm of a crypto key, in lower case.
    /// </summary>
    internal const string AlgorithmLabelKey = "ncode-identity-algorithm";

    private static readonly TimeSpan InitialPollDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxPollDelay = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(60);

    // The wire name of each algorithm, such as RSA_SIGN_PSS_3072_SHA256, is the original name of the enum member in
    // the protocol buffer definition.
    private static readonly FrozenDictionary<
        string,
        CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm
    > KeySpecs = CreateKeySpecs();

    private IGoogleCloudKmsClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<GoogleCloudKmsRemoteKeyProviderOptions> ProviderOptions { get; } = options;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public string Name => RemoteKeyProviderNames.GoogleCloudKms;

    /// <summary>
    /// Maps the wire name of a Cloud KMS algorithm to the member of the client library's enumeration.
    /// </summary>
    /// <param name="keySpec">The wire name of the algorithm, such as <c>RSA_SIGN_PSS_3072_SHA256</c>.</param>
    /// <param name="algorithm">The member that has the wire name.</param>
    /// <returns><see langword="true"/> if a member has the wire name; otherwise, <see langword="false"/>.</returns>
    internal static bool TryGetAlgorithm(
        string keySpec,
        out CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm algorithm
    ) => KeySpecs.TryGetValue(keySpec, out algorithm);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured key ring is not a Cloud KMS key ring name, or when the service fails to generate the
    /// key version or does not enable it within 60 seconds.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the key specification of the algorithm of <paramref name="request"/> is not a Cloud KMS algorithm.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service creates a key version that does not belong to the crypto key, reports a public key of
    /// another algorithm, or returns a public key that is not a PEM-encoded <c>SubjectPublicKeyInfo</c>.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service does not confirm the integrity of the public key.
    /// </exception>
    public async ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var keyRing = ProviderOptions.Value.KeyRing;
        if (!GoogleCloudKmsKeyName.IsKeyRingName(keyRing))
        {
            throw new InvalidOperationException(
                "SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing is not a Cloud KMS key ring name."
            );
        }

        var row = request.Algorithm;
        if (!KeySpecs.TryGetValue(row.KeySpec, out var algorithm))
        {
            throw new ArgumentException(
                $"The key specification '{row.KeySpec}' is not a Cloud KMS algorithm.",
                nameof(request)
            );
        }

        var cryptoKeyName = $"{keyRing}/cryptoKeys/{GetCryptoKeyId(request)}";

        var version = await CreateVersionAsync(cryptoKeyName, request, cancellationToken);

        var versionName = version.Name;
        if (
            !GoogleCloudKmsKeyName.IsCryptoKeyVersionName(versionName)
            || !string.Equals(
                GoogleCloudKmsKeyName.GetCryptoKeyName(versionName),
                cryptoKeyName,
                StringComparison.Ordinal
            )
        )
        {
            throw new CryptographicException(
                $"Google Cloud KMS created '{versionName}', which is not a version of the crypto key '{cryptoKeyName}'."
            );
        }

        await WaitUntilEnabledAsync(versionName, cancellationToken);

        var publicKey = await GetPublicKeyAsync(versionName, algorithm, cancellationToken);

        return new RemoteKeyCreationResult
        {
            KeyId = versionName,
            SubjectPublicKeyInfo = publicKey,
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key identifier of <paramref name="request"/> is not a Cloud KMS crypto key version name, or the
    /// algorithm of the request names a digest that Cloud KMS does not accept.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service does not confirm the integrity of the request and response, or reports that another
    /// key version made the signature.
    /// </exception>
    public async ValueTask<byte[]> SignAsync(
        RemoteSignatureRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var keyId = request.KeyId;
        if (!GoogleCloudKmsKeyName.IsCryptoKeyVersionName(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not the name of a Cloud KMS crypto key version.",
                nameof(request)
            );
        }

        var data = request.Data;
        var checksum = GoogleCloudKmsChecksum.Compute(data.Span);

        // The call only reads the caller's buffer and it outlives the call, so it is wrapped without a copy.
        var signRequest = new AsymmetricSignRequest { Name = keyId };
        var isDigest = request.Algorithm.DigestAlgorithm is not null;
        if (request.Algorithm.DigestAlgorithm is { } digestAlgorithm)
        {
            signRequest.Digest = CreateDigest(
                digestAlgorithm,
                UnsafeByteOperations.UnsafeWrap(data)
            );
            signRequest.DigestCrc32C = checksum;
        }
        else
        {
            signRequest.Data = UnsafeByteOperations.UnsafeWrap(data);
            signRequest.DataCrc32C = checksum;
        }

        var callSettings = CreateCallSettings(
            GoogleCloudKmsCallSettings.Retrying,
            cancellationToken
        );

        var response = await ClientFactory.ExecuteAsync(
            access: null,
            (client, _) =>
                new ValueTask<AsymmetricSignResponse>(
                    client.AsymmetricSignAsync(signRequest, callSettings)
                ),
            cancellationToken
        );

        var isRequestVerified = isDigest
            ? response.VerifiedDigestCrc32C
            : response.VerifiedDataCrc32C;

        if (
            !isRequestVerified
            || response.SignatureCrc32C != GoogleCloudKmsChecksum.Compute(response.Signature.Span)
        )
        {
            throw new IOException(
                $"Google Cloud KMS did not confirm the integrity of the sign request and response for '{keyId}'; the call can be retried."
            );
        }

        if (!string.Equals(response.Name, keyId, StringComparison.Ordinal))
        {
            throw new IOException(
                $"Google Cloud KMS key version '{keyId}' was asked to sign, but the service reports that '{response.Name}' made the signature; the call can be retried."
            );
        }

        return response.Signature.ToByteArray();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="keyId"/> is not a Cloud KMS crypto key version name.
    /// </exception>
    public async ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keyId);

        if (!GoogleCloudKmsKeyName.IsCryptoKeyVersionName(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not the name of a Cloud KMS crypto key version.",
                nameof(keyId)
            );
        }

        var destroyRequest = new DestroyCryptoKeyVersionRequest { Name = keyId };
        var callSettings = CreateCallSettings(
            GoogleCloudKmsCallSettings.Creating,
            cancellationToken
        );

        try
        {
            await ClientFactory.ExecuteAsync(
                access: null,
                (client, _) =>
                    new ValueTask<CryptoKeyVersion>(
                        client.DestroyCryptoKeyVersionAsync(destroyRequest, callSettings)
                    ),
                cancellationToken
            );
        }
        catch (RpcException exception)
            when (exception.StatusCode is StatusCode.NotFound or StatusCode.FailedPrecondition)
        {
            // The version is already missing, or it is not in a state that can be destroyed because it is already
            // scheduled for destruction or destroyed.
        }
    }

    // The suffix separates the crypto keys of the two protection levels, so a change of the option never adds a version
    // of another protection level to an existing crypto key.
    private string GetCryptoKeyId(RemoteKeyCreationRequest request) =>
        $"{request.GroupName}-{(ProviderOptions.Value.HardwareProtected ? 'h' : 's')}";

    private async ValueTask<CryptoKeyVersion> CreateVersionAsync(
        string cryptoKeyName,
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await TryCreateVersionAsync(cryptoKeyName, cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            // The first key of the group creates its crypto key, which holds the algorithm and protection level of
            // every version that follows.
        }

        await CreateCryptoKeyAsync(request, cancellationToken);

        return await TryCreateVersionAsync(cryptoKeyName, cancellationToken);
    }

    private async ValueTask<CryptoKeyVersion> TryCreateVersionAsync(
        string cryptoKeyName,
        CancellationToken cancellationToken
    )
    {
        var versionRequest = new CreateCryptoKeyVersionRequest
        {
            Parent = cryptoKeyName,
            CryptoKeyVersion = new CryptoKeyVersion(),
        };
        var callSettings = CreateCallSettings(
            GoogleCloudKmsCallSettings.Creating,
            cancellationToken
        );

        return await ClientFactory.ExecuteAsync(
            access: null,
            (client, _) =>
                new ValueTask<CryptoKeyVersion>(
                    client.CreateCryptoKeyVersionAsync(versionRequest, callSettings)
                ),
            cancellationToken
        );
    }

    private async ValueTask CreateCryptoKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var cryptoKey = new CryptoKey
        {
            Purpose = CryptoKey.Types.CryptoKeyPurpose.AsymmetricSign,
            VersionTemplate = new CryptoKeyVersionTemplate
            {
                Algorithm = KeySpecs[request.Algorithm.KeySpec],
                ProtectionLevel = ProviderOptions.Value.HardwareProtected
                    ? ProtectionLevel.Hsm
                    : ProtectionLevel.Software,
            },
            Labels =
            {
                { GroupLabelKey, request.GroupName },
                { AlgorithmLabelKey, request.Algorithm.Algorithm.ToLowerInvariant() },
            },
        };

        var cryptoKeyRequest = new CreateCryptoKeyRequest
        {
            Parent = ProviderOptions.Value.KeyRing,
            CryptoKeyId = GetCryptoKeyId(request),
            CryptoKey = cryptoKey,
            SkipInitialVersionCreation = true,
        };
        var callSettings = CreateCallSettings(
            GoogleCloudKmsCallSettings.Creating,
            cancellationToken
        );

        try
        {
            await ClientFactory.ExecuteAsync(
                access: null,
                (client, _) =>
                    new ValueTask<CryptoKey>(
                        client.CreateCryptoKeyAsync(cryptoKeyRequest, callSettings)
                    ),
                cancellationToken
            );
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            // Another creation of a key of the group made the crypto key first.
        }
    }

    private async ValueTask WaitUntilEnabledAsync(
        string versionName,
        CancellationToken cancellationToken
    )
    {
        var versionRequest = new GetCryptoKeyVersionRequest { Name = versionName };
        var callSettings = CreateCallSettings(
            GoogleCloudKmsCallSettings.Retrying,
            cancellationToken
        );

        var waited = TimeSpan.Zero;
        var nextDelay = InitialPollDelay;

        while (true)
        {
            var version = await ClientFactory.ExecuteAsync(
                access: null,
                (client, _) =>
                    new ValueTask<CryptoKeyVersion>(
                        client.GetCryptoKeyVersionAsync(versionRequest, callSettings)
                    ),
                cancellationToken
            );

            switch (version.State)
            {
                case CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled:
                    return;

                case CryptoKeyVersion.Types.CryptoKeyVersionState.GenerationFailed:
                    throw new InvalidOperationException(
                        $"Google Cloud KMS failed to generate the key version '{versionName}'."
                    );
            }

            if (waited >= PollTimeout)
            {
                throw new InvalidOperationException(
                    $"Google Cloud KMS did not enable the key version '{versionName}' within {PollTimeout.TotalSeconds:0} seconds."
                );
            }

            var delay = TimeSpan.FromTicks(Math.Min(nextDelay.Ticks, (PollTimeout - waited).Ticks));
            await Task.Delay(delay, TimeProvider, cancellationToken);

            waited += delay;
            nextDelay = TimeSpan.FromTicks(Math.Min(nextDelay.Ticks * 2, MaxPollDelay.Ticks));
        }
    }

    private async ValueTask<byte[]> GetPublicKeyAsync(
        string versionName,
        CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm algorithm,
        CancellationToken cancellationToken
    )
    {
        var publicKeyRequest = new GetPublicKeyRequest { Name = versionName };
        var callSettings = CreateCallSettings(
            GoogleCloudKmsCallSettings.Retrying,
            cancellationToken
        );

        var publicKey = await ClientFactory.ExecuteAsync(
            access: null,
            (client, _) =>
                new ValueTask<PublicKey>(client.GetPublicKeyAsync(publicKeyRequest, callSettings)),
            cancellationToken
        );

        var pem = publicKey.Pem;
        if (
            !string.Equals(publicKey.Name, versionName, StringComparison.Ordinal)
            || publicKey.PemCrc32C
                != GoogleCloudKmsChecksum.Compute(SecureEncoding.UTF8.GetBytes(pem))
        )
        {
            throw new IOException(
                $"Google Cloud KMS did not confirm the integrity of the public key of '{versionName}'; the call can be retried."
            );
        }

        if (publicKey.Algorithm != algorithm)
        {
            throw new CryptographicException(
                $"Google Cloud KMS key version '{versionName}' does not have the algorithm '{algorithm}'."
            );
        }

        if (
            !PemEncoding.TryFind(pem, out var fields)
            || !pem.AsSpan(fields.Label).SequenceEqual("PUBLIC KEY")
        )
        {
            throw new CryptographicException(
                $"Google Cloud KMS key version '{versionName}' reported a public key that is not a PEM-encoded SubjectPublicKeyInfo."
            );
        }

        try
        {
            return Convert.FromBase64String(pem[fields.Base64Data]);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(
                $"Google Cloud KMS key version '{versionName}' reported a public key that is not valid base64 text.",
                exception
            );
        }
    }

    private static Digest CreateDigest(HashAlgorithmName digestAlgorithm, ByteString digest)
    {
        // Cloud KMS accepts a digest in one of three fields, one for each hash algorithm that its signing algorithms
        // use, so the switch is closed over them.
        if (digestAlgorithm == HashAlgorithmName.SHA256)
        {
            return new Digest { Sha256 = digest };
        }

        if (digestAlgorithm == HashAlgorithmName.SHA384)
        {
            return new Digest { Sha384 = digest };
        }

        if (digestAlgorithm == HashAlgorithmName.SHA512)
        {
            return new Digest { Sha512 = digest };
        }

        throw new ArgumentException(
            $"Cloud KMS does not sign a digest made with '{digestAlgorithm}'.",
            nameof(digestAlgorithm)
        );
    }

    private static CallSettings CreateCallSettings(
        CallSettings settings,
        CancellationToken cancellationToken
    ) => settings.MergedWith(CallSettings.FromCancellationToken(cancellationToken));

    private static FrozenDictionary<
        string,
        CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm
    > CreateKeySpecs()
    {
        var keySpecs = new Dictionary<string, CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm>(
            StringComparer.Ordinal
        );

        foreach (
            var algorithm in Enum.GetValues<CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm>()
        )
        {
            var originalName = typeof(CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm)
                .GetField(algorithm.ToString())
                ?.GetCustomAttribute<OriginalNameAttribute>()
                ?.Name;

            if (originalName is not null)
            {
                keySpecs[originalName] = algorithm;
            }
        }

        return keySpecs.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
