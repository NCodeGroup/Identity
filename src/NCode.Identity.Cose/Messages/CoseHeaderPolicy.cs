// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
