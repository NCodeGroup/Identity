// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Holds the rules for the header buckets of a COSE layer (RFC 9052 §3): the shape of each known parameter, the
/// placement and the understanding of critical parameters, and the buckets being disjoint.
/// </summary>
// See ADR-1409.
internal static class CoseHeaderPolicy
{
    /// <summary>
    /// The labels of the parameters that this library itself understands for <c>crit</c>.
    /// </summary>
    private static readonly long[] UnderstoodLabels =
    [
        CoseHeaderLabels.Algorithm,
        CoseHeaderLabels.Critical,
        CoseHeaderLabels.ContentType,
        CoseHeaderLabels.KeyId,
        CoseHeaderLabels.InitializationVector,
        CoseHeaderLabels.PartialInitializationVector,
    ];

    private static CoseException Invalid(string message) =>
        new(CoseFailureCodes.InvalidHeader, message);

    /// <summary>
    /// Checks the two buckets of a layer that was received: disjoint, known parameters of their defined shapes, a
    /// <c>crit</c> parameter that is protected and lists protected parameters, and no <c>Partial IV</c>.
    /// </summary>
    public static void ValidateRead(
        IReadOnlyDictionary<CoseLabel, CoseValue> protectedHeaders,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders
    )
    {
        foreach (var label in protectedHeaders.Keys)
        {
            if (unprotectedHeaders.ContainsKey(label))
                throw Invalid("A header parameter is present in both header buckets.");
        }

        foreach (var (label, value) in protectedHeaders)
            ValidateShape(label, value);

        foreach (var (label, value) in unprotectedHeaders)
            ValidateShape(label, value);

        if (
            protectedHeaders.ContainsKey(CoseHeaderLabels.PartialInitializationVector)
            || unprotectedHeaders.ContainsKey(CoseHeaderLabels.PartialInitializationVector)
        )
            throw Invalid("A Partial IV needs a Base IV, which this library does not support.");

        if (unprotectedHeaders.ContainsKey(CoseHeaderLabels.Critical))
            throw Invalid("The crit parameter is not in the protected header bucket.");

        if (protectedHeaders.TryGetValue(CoseHeaderLabels.Critical, out var critical))
        {
            foreach (var label in ReadCritical(critical))
            {
                if (!protectedHeaders.ContainsKey(label))
                    throw Invalid("The crit parameter lists a parameter that is not protected.");
            }
        }
    }

    /// <summary>
    /// Refuses a layer whose <c>crit</c> parameter lists a label that neither this library nor the caller understands.
    /// </summary>
    public static void EnsureCriticalUnderstood(
        IReadOnlyDictionary<CoseLabel, CoseValue> protectedHeaders,
        IReadOnlyCollection<CoseLabel> understoodByCaller
    )
    {
        if (!protectedHeaders.TryGetValue(CoseHeaderLabels.Critical, out var critical))
            return;

        foreach (var label in ReadCritical(critical))
        {
            if (
                !label.IsText && Array.IndexOf(UnderstoodLabels, label.IntegerValue) >= 0
                || understoodByCaller.Contains(label)
            )
                continue;

            throw new CoseException(
                CoseFailureCodes.CriticalHeaderNotUnderstood,
                "The message requires a header parameter that the verifier does not understand."
            );
        }
    }

    /// <summary>
    /// Checks the header parameters a caller supplies for a message to encode: the library owns <c>alg</c>, <c>IV</c> and
    /// <c>Partial IV</c>, every value is a bounded definite-length item, and the buckets obey the rules of a received
    /// message.
    /// </summary>
    /// <exception cref="ArgumentException">A parameter is owned by the library or invalid.</exception>
    public static void ValidateWrite(
        IReadOnlyDictionary<CoseLabel, CoseValue> protectedHeaders,
        IReadOnlyDictionary<CoseLabel, CoseValue> unprotectedHeaders,
        int maxNestingDepth
    )
    {
        ArgumentNullException.ThrowIfNull(protectedHeaders);
        ArgumentNullException.ThrowIfNull(unprotectedHeaders);

        foreach (var bucket in new[] { protectedHeaders, unprotectedHeaders })
        {
            foreach (var (label, value) in bucket)
            {
                if (
                    !label.IsText
                    && label.IntegerValue
                        is CoseHeaderLabels.Algorithm
                            or CoseHeaderLabels.InitializationVector
                            or CoseHeaderLabels.PartialInitializationVector
                )
                    throw new ArgumentException(
                        "The alg, IV and Partial IV header parameters are set by the library.",
                        nameof(protectedHeaders)
                    );

                CborMapWriter.ValidateValue(value, maxNestingDepth - 1);
            }
        }

        try
        {
            ValidateRead(protectedHeaders, unprotectedHeaders);
        }
        catch (CoseException exception)
        {
            throw new ArgumentException(
                "The header parameters do not satisfy the rules for COSE header buckets.",
                nameof(protectedHeaders),
                exception
            );
        }
    }

    /// <summary>
    /// Resolves the algorithm of a message body or signature layer. The algorithm must be in the protected bucket, must
    /// be one the caller permits, and must be a registered algorithm of the kind the layer needs; the permitted check
    /// runs before the catalog is consulted.
    /// </summary>
    /// <exception cref="CoseException">The algorithm is missing, unprotected, not permitted, unregistered or of
    /// another kind.</exception>
    public static CoseAlgorithmBinding ResolveLayerAlgorithm(
        ParsedCoseLayer layer,
        IReadOnlyCollection<int> permitted,
        CoseAlgorithmKind kind,
        ICoseAlgorithmCatalog catalog
    )
    {
        if (!layer.Protected.TryGetValue(CoseHeaderLabels.Algorithm, out var value))
        {
            throw layer.Unprotected.ContainsKey(CoseHeaderLabels.Algorithm)
                ? new CoseException(
                    CoseFailureCodes.UnprotectedAlgorithm,
                    "The algorithm of the message is not in the protected header bucket."
                )
                : Invalid("The message names no algorithm.");
        }

        if (value.TryGetTextString(out _))
            throw new CoseException(
                CoseFailureCodes.UnsupportedAlgorithm,
                "The message names its algorithm with text, which is not supported."
            );

        if (
            !value.TryGetInt64(out var identifier)
            || identifier is < int.MinValue or > int.MaxValue
            || !permitted.Contains((int)identifier)
        )
            throw new CoseException(
                CoseFailureCodes.AlgorithmNotPermitted,
                "The algorithm of the message is not permitted."
            );

        if (!catalog.TryGet((int)identifier, out var binding) || binding.Algorithm.Kind != kind)
            throw new CoseException(
                CoseFailureCodes.UnsupportedAlgorithm,
                "The algorithm of the message is not registered for this use."
            );

        return binding;
    }

    private static List<CoseLabel> ReadCritical(CoseValue value)
    {
        if (!value.TryGetArray(out var elements) || elements.Count == 0)
            throw Invalid("The crit parameter is not a non-empty array.");

        var labels = new List<CoseLabel>(elements.Count);
        foreach (var element in elements)
        {
            if (element.TryGetInt64(out var integer))
                labels.Add(new CoseLabel(integer));
            else if (element.TryGetTextString(out var text))
                labels.Add(new CoseLabel(text));
            else
                throw Invalid(
                    "The crit parameter lists a label that is neither an integer nor text."
                );
        }

        return labels;
    }

    private static void ValidateShape(CoseLabel label, CoseValue value)
    {
        if (label.IsText)
            return;

        switch (label.IntegerValue)
        {
            case CoseHeaderLabels.Algorithm:
                if (!value.TryGetInt64(out _) && !value.TryGetTextString(out _))
                    throw Invalid("The alg parameter is neither an integer nor text.");

                break;
            case CoseHeaderLabels.Critical:
                _ = ReadCritical(value);
                break;
            case CoseHeaderLabels.ContentType:
            case CoseHeaderLabels.Type:
                if (
                    !(value.TryGetInt64(out var number) && number >= 0)
                    && !value.TryGetTextString(out _)
                )
                    throw Invalid(
                        "A content type or typ parameter is neither an unsigned integer nor text."
                    );

                break;
            case CoseHeaderLabels.KeyId:
            case CoseHeaderLabels.InitializationVector:
            case CoseHeaderLabels.PartialInitializationVector:
                if (!value.TryGetByteString(out _))
                    throw Invalid("A kid, IV or Partial IV parameter is not a byte string.");

                break;
        }
    }
}
