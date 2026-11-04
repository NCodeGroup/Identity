// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Options;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ICoseKeyConverter"/>: a COSE_Key is mapped onto a JSON Web Key and
/// imported through the registered secret key types, so every asymmetric key type the JOSE stack serves can be read.
/// </summary>
// See ADR-1409.
internal class DefaultCoseKeyConverter(
    ICoseKeyTypeCatalog keyTypeCatalog,
    ICoseAlgorithmCatalog algorithmCatalog,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IOptions<CoseOptions> options
) : ICoseKeyConverter
{
    private ICoseKeyTypeCatalog KeyTypeCatalog { get; } = keyTypeCatalog;
    private ICoseAlgorithmCatalog AlgorithmCatalog { get; } = algorithmCatalog;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
    private CoseOptions Options { get; } = options.Value;

    private static CoseException InvalidKey(string message, Exception? inner = null) =>
        inner is null
            ? new CoseException(CoseFailureCodes.InvalidKey, message)
            : new CoseException(CoseFailureCodes.InvalidKey, message, inner);

    private static CoseException Unsupported(string code, string message) => new(code, message);

    /// <inheritdoc />
    public SecretKey ReadPublicKey(ReadOnlyMemory<byte> encodedKey) =>
        ConvertMap(CborGuard.ReadRootMap(encodedKey, Options));

    /// <inheritdoc />
    public IReadOnlyList<SecretKey> ReadPublicKeySet(ReadOnlyMemory<byte> encodedKeySet)
    {
        CborGuard.CheckSize(encodedKeySet, Options);

        var maps = new List<IReadOnlyDictionary<CoseLabel, CoseValue>>();
        try
        {
            var reader = CborGuard.NewReader(encodedKeySet);
            if (reader.PeekState() != CborReaderState.StartArray)
                throw CborGuard.Invalid("The COSE_KeySet is not an array.");

            var length =
                reader.ReadStartArray()
                ?? throw CborGuard.Invalid("The COSE_KeySet uses an indefinite length.");
            if (Options.MaxNestingDepth < 2)
                throw new CoseException(
                    CoseFailureCodes.LimitExceeded,
                    "The CBOR data item is nested deeper than the limit."
                );

            for (var index = 0; index < length; index++)
                maps.Add(CborGuard.ReadMap(reader, Options.MaxNestingDepth - 1));

            reader.ReadEndArray();
            if (reader.BytesRemaining != 0)
                throw CborGuard.Invalid("The input has bytes after the COSE_KeySet.");
        }
        catch (Exception exception) when (CborGuard.IsReaderException(exception))
        {
            throw CborGuard.Invalid("The COSE_KeySet is malformed.", exception);
        }

        var keys = new List<SecretKey>(maps.Count);
        foreach (var map in maps)
        {
            try
            {
                keys.Add(ConvertMap(map));
            }
            catch (CoseException exception)
                when (exception.FailureCode
                        is CoseFailureCodes.UnsupportedKeyType
                            or CoseFailureCodes.UnsupportedAlgorithm
                )
            {
                // A key this host cannot use is skipped, as a JSON Web Key Set reader skips one (RFC 7517 §5).
            }
        }

        return keys;
    }

    /// <inheritdoc />
    public byte[] WritePublicKey(SecretKey secretKey) =>
        CborMapWriter.EncodeMap(BuildMap(secretKey));

    /// <inheritdoc />
    public byte[] WritePublicKeySet(IEnumerable<SecretKey> secretKeys)
    {
        ArgumentNullException.ThrowIfNull(secretKeys);

        var maps = secretKeys.Select(BuildMap).ToList();
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(maps.Count);
        foreach (var map in maps)
            CborMapWriter.WriteMap(writer, map);

        writer.WriteEndArray();
        return writer.Encode();
    }

    private Dictionary<CoseLabel, CoseValue> BuildMap(SecretKey secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);

        if (
            !SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var secretKeyDescriptor)
            || secretKeyDescriptor.ExportPublicKey(secretKey) is not { } exported
        )
            throw new ArgumentException(
                "The key has no public form and cannot be written as a COSE_Key.",
                nameof(secretKey)
            );

        var jwk = exported.Jwk;
        if (!KeyTypeCatalog.TryGetKeyTypeByJwkKeyType(jwk.KeyType, out var descriptor))
            throw new ArgumentException(
                "The key type of the key is not a registered COSE key type.",
                nameof(secretKey)
            );

        var map = new Dictionary<CoseLabel, CoseValue>
        {
            [CoseKeyParameterLabels.KeyType] = CoseValue.FromInt64(descriptor.KeyType),
        };

        if (!string.IsNullOrEmpty(secretKey.KeyId))
            map[CoseKeyParameterLabels.KeyId] = CoseValue.FromByteString(
                SecureEncoding.UTF8.GetBytes(secretKey.KeyId)
            );

        if (descriptor.RequiresAlgorithm)
        {
            var matches = AlgorithmCatalog
                .Algorithms.Where(binding =>
                    binding.Algorithm.Kind == CoseAlgorithmKind.Signature
                    && string.Equals(
                        binding.JoseAlgorithm.Code,
                        jwk.Algorithm,
                        StringComparison.Ordinal
                    )
                )
                .ToList();
            if (matches.Count != 1)
                throw new ArgumentException(
                    "The algorithm of the key does not name exactly one registered COSE algorithm.",
                    nameof(secretKey)
                );

            map[CoseKeyParameterLabels.Algorithm] = CoseValue.FromInt64(
                matches[0].Algorithm.Identifier
            );
        }

        foreach (var parameter in descriptor.Parameters)
        {
            if (parameter.IsPrivate || jwk.GetMember(parameter.JwkMember) is not { } member)
                continue;

            switch (parameter.Encoding)
            {
                case CoseKeyParameterEncoding.ByteString:
                    try
                    {
                        map[parameter.Label] = CoseValue.FromByteString(
                            Base64Url.DecodeFromChars(member)
                        );
                    }
                    catch (FormatException exception)
                    {
                        throw new ArgumentException(
                            "A member of the key is not base64url text.",
                            nameof(secretKey),
                            exception
                        );
                    }

                    break;
                default:
                    if (
                        !KeyTypeCatalog.TryGetCurveByJwkName(
                            descriptor.KeyType,
                            member,
                            out var curve
                        )
                    )
                        throw new ArgumentException(
                            "The curve of the key is not a registered COSE curve.",
                            nameof(secretKey)
                        );

                    map[parameter.Label] = CoseValue.FromInt64(curve.Identifier);
                    break;
            }
        }

        return map;
    }

    private SecretKey ConvertMap(IReadOnlyDictionary<CoseLabel, CoseValue> map)
    {
        if (!map.TryGetValue(CoseKeyParameterLabels.KeyType, out var ktyValue))
            throw InvalidKey("The COSE_Key has no key type.");

        if (ktyValue.TryGetTextString(out _))
            throw Unsupported(
                CoseFailureCodes.UnsupportedKeyType,
                "The key type of the COSE_Key is not registered."
            );

        if (!ktyValue.TryGetInt64(out var ktyLong))
            throw InvalidKey("The key type of the COSE_Key is not an integer.");

        if (
            ktyLong is < int.MinValue or > int.MaxValue
            || !KeyTypeCatalog.TryGetKeyType((int)ktyLong, out var descriptor)
        )
            throw Unsupported(
                CoseFailureCodes.UnsupportedKeyType,
                "The key type of the COSE_Key is not registered."
            );

        string? keyId = null;
        if (map.TryGetValue(CoseKeyParameterLabels.KeyId, out var kidValue))
        {
            if (!kidValue.TryGetByteString(out var kidBytes))
                throw InvalidKey("The key identifier of the COSE_Key is not a byte string.");

            try
            {
                keyId = SecureEncoding.UTF8.GetString(kidBytes.Span);
            }
            catch (ArgumentException exception)
            {
                throw InvalidKey(
                    "The key identifier of the COSE_Key is not UTF-8 text.",
                    exception
                );
            }
        }

        string? joseAlgorithm = null;
        if (map.TryGetValue(CoseKeyParameterLabels.Algorithm, out var algValue))
        {
            if (
                !algValue.TryGetInt64(out var algLong)
                || algLong is < int.MinValue or > int.MaxValue
                || !AlgorithmCatalog.TryGet((int)algLong, out var binding)
            )
                throw Unsupported(
                    CoseFailureCodes.UnsupportedAlgorithm,
                    "The algorithm of the COSE_Key is not registered."
                );

            joseAlgorithm = binding.Algorithm.JoseAlgorithmCode;
        }

        string[]? keyOperations = null;
        if (map.TryGetValue(CoseKeyParameterLabels.KeyOperations, out var opsValue))
            keyOperations = ReadKeyOperations(opsValue);

        if (map.ContainsKey(CoseKeyParameterLabels.BaseIv))
            throw InvalidKey("The COSE_Key carries a Base IV, which a public key does not use.");

        if (descriptor.RequiresAlgorithm && joseAlgorithm is null)
            throw InvalidKey("The COSE_Key does not carry the algorithm its key type requires.");

        var members = new JsonObject { [JsonWebKeyParameterNames.KeyType] = descriptor.JwkKeyType };
        foreach (var parameter in descriptor.Parameters)
        {
            if (!map.TryGetValue(parameter.Label, out var value))
                continue;

            if (parameter.IsPrivate)
                throw InvalidKey("The COSE_Key carries private key material.");

            if (parameter.Encoding == CoseKeyParameterEncoding.ByteString)
            {
                if (!value.TryGetByteString(out var bytes))
                    throw InvalidKey("A parameter of the COSE_Key is not a byte string.");

                members[parameter.JwkMember] = Base64Url.EncodeToString(bytes.Span);
            }
            else
            {
                if (!value.TryGetInt64(out var curveLong))
                    throw InvalidKey("The curve of the COSE_Key is not an integer.");

                if (
                    curveLong is < int.MinValue or > int.MaxValue
                    || !KeyTypeCatalog.TryGetCurve(
                        descriptor.KeyType,
                        (int)curveLong,
                        out var curve
                    )
                )
                    throw Unsupported(
                        CoseFailureCodes.UnsupportedKeyType,
                        "The curve of the COSE_Key is not registered."
                    );

                members[parameter.JwkMember] = curve.JwkCurveName;
            }
        }

        if (keyId is not null)
            members[JsonWebKeyParameterNames.KeyId] = keyId;

        if (joseAlgorithm is not null)
            members[JsonWebKeyParameterNames.Algorithm] = joseAlgorithm;

        if (keyOperations is not null)
        {
            var operations = new JsonArray();
            foreach (var operation in keyOperations)
                operations.Add(operation);

            members[JsonWebKeyParameterNames.KeyOperations] = operations;
        }

        return Import(descriptor, members, keyId, joseAlgorithm, keyOperations);
    }

    private static string[] ReadKeyOperations(CoseValue value)
    {
        if (!value.TryGetArray(out var elements))
            throw InvalidKey("The key operations of the COSE_Key are not an array.");

        var operations = new List<string>(elements.Count);
        foreach (var element in elements)
        {
            if (!element.TryGetInt64(out var operation))
                throw InvalidKey("A key operation of the COSE_Key is not an integer.");

            operations.Add(
                operation switch
                {
                    CoseKeyOperations.Sign => JsonWebKeyOperations.Sign,
                    CoseKeyOperations.Verify => JsonWebKeyOperations.Verify,
                    CoseKeyOperations.Encrypt => JsonWebKeyOperations.Encrypt,
                    CoseKeyOperations.Decrypt => JsonWebKeyOperations.Decrypt,
                    CoseKeyOperations.WrapKey => JsonWebKeyOperations.WrapKey,
                    CoseKeyOperations.UnwrapKey => JsonWebKeyOperations.UnwrapKey,
                    CoseKeyOperations.DeriveKey => JsonWebKeyOperations.DeriveKey,
                    CoseKeyOperations.DeriveBits => JsonWebKeyOperations.DeriveBits,
                    _ => throw InvalidKey(
                        "A key operation of the COSE_Key is not valid for a public key."
                    ),
                }
            );
        }

        return [.. operations];
    }

    private SecretKey Import(
        CoseKeyTypeDescriptor descriptor,
        JsonObject members,
        string? keyId,
        string? joseAlgorithm,
        string[]? keyOperations
    )
    {
        if (
            !SecretKeyTypeCatalog.TryGetByJwkKeyType(
                descriptor.JwkKeyType,
                out var secretKeyDescriptor
            )
            || !secretKeyDescriptor.IsAsymmetric
            || secretKeyDescriptor.JwkKeyType is not { } jwkKeyType
            || !secretKeyDescriptor.PublicKeyImportFormats.Contains(SecretKeyImportFormats.Jwk)
        )
            throw Unsupported(
                CoseFailureCodes.UnsupportedKeyType,
                "No registered secret key type imports a public COSE_Key of this key type."
            );

        IDisposable? lease = null;
        try
        {
            var jwk = new JsonWebKey(members, jwkKeyType);
            if (jwk.ContainsPrivateKeyMaterial)
                throw InvalidKey("The COSE_Key carries private key material.");

            // The import hands the public key bytes to a callback and reports their encoding only once it returns, so
            // the bytes are held in a rented buffer until the key is deserialized.
            Memory<byte> keyMaterial = default;
            var imported = secretKeyDescriptor.ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = jwk.ToJsonString(),
                },
                bytes =>
                {
                    lease?.Dispose();
                    lease = BufferFactory.Rent(bytes.Length, isSensitive: false, out keyMaterial);
                    bytes.CopyTo(keyMaterial.Span);
                    return string.Empty;
                }
            );

            return secretKeyDescriptor.DeserializeKeyMaterial(
                new KeyMetadata
                {
                    KeyId = keyId,
                    Algorithm = joseAlgorithm,
                    Use = ResolveUse(keyOperations),
                },
                imported.SecretEncoding,
                keyMaterial.Span
            );
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
            throw InvalidKey("The COSE_Key cannot be imported as a public key.", exception);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private static string? ResolveUse(string[]? keyOperations)
    {
        if (keyOperations is not { Length: > 0 })
            return null;

        if (
            keyOperations.All(operation =>
                operation is JsonWebKeyOperations.Sign or JsonWebKeyOperations.Verify
            )
        )
            return SecretKeyUses.Signature;

        return keyOperations.All(operation =>
            operation
                is JsonWebKeyOperations.Encrypt
                    or JsonWebKeyOperations.Decrypt
                    or JsonWebKeyOperations.WrapKey
                    or JsonWebKeyOperations.UnwrapKey
                    or JsonWebKeyOperations.DeriveKey
                    or JsonWebKeyOperations.DeriveBits
        )
            ? SecretKeyUses.Encryption
            : null;
    }
}
