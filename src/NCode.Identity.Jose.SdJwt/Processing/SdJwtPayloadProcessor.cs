// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Profiles;
using NCode.Identity.Jose.SdJwt.Verification;

namespace NCode.Identity.Jose.SdJwt.Processing;

/// <summary>
/// Resolves the disclosures of a Selective Disclosure JWT into its issuer-signed JWT payload as RFC 9901 §7.1
/// requires: every digest is matched to at most one disclosure, a disclosed value is itself resolved so nested
/// disclosures land where the issuer put them, a digest that matches no disclosure is a decoy and is dropped, and the
/// presentation is refused when a digest repeats, a disclosure is of the wrong kind for its digest, a disclosed claim
/// collides with one already present, a reserved name appears where it may not, a disclosure is referenced by
/// nothing, or a digest discloses a protected top-level claim or any part of one: a protected claim is plaintext in
/// its entirety, because a withheld disclosure beneath it would be indistinguishable from a decoy.
/// </summary>
/// <param name="algorithm">The hash algorithm of the digests.</param>
/// <param name="maxNestingDepth">The deepest nesting of objects and arrays the resolved claims may reach.</param>
/// <param name="profile">The profile whose protected claims join the format's own, or <c>null</c>.</param>
// See ADR-1405.
internal sealed class SdJwtPayloadProcessor(
    SdJwtDigestAlgorithm algorithm,
    int maxNestingDepth,
    SdJwtProfile? profile
)
{
    private const string RootPath = "";

    private SdJwtDigestAlgorithm Algorithm { get; } = algorithm;
    private int MaxNestingDepth { get; } = maxNestingDepth;
    private SdJwtProfile? Profile { get; } = profile;

    private Dictionary<string, Disclosure> DisclosuresByDigest { get; } =
        new(StringComparer.Ordinal);
    private HashSet<string> SeenDigests { get; } = new(StringComparer.Ordinal);
    private List<DisclosedClaim> Disclosed { get; } = [];

    /// <summary>
    /// Resolves the disclosures into the payload.
    /// </summary>
    /// <param name="payload">The issuer-signed JWT payload, a JSON object.</param>
    /// <param name="disclosures">The presented disclosures.</param>
    /// <returns>The resolved claims and where each disclosure landed.</returns>
    /// <exception cref="SdJwtException">The presentation violates a rule of RFC 9901 §7.1.</exception>
    public ProcessedSdJwtPayload Process(JsonElement payload, IReadOnlyList<Disclosure> disclosures)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT payload must be a JSON object."
            );

        var count = disclosures.Count;
        for (var i = 0; i < count; i++)
        {
            var disclosure = disclosures[i];
            var digest = disclosure.ComputeDigest(Algorithm);
            if (!DisclosuresByDigest.TryAdd(digest, disclosure))
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.DuplicateDisclosure,
                    "Two presented disclosures have the same digest."
                );
        }

        JsonObject claims;
        try
        {
            claims = ToObject(payload);
            ProcessObject(claims, RootPath, depth: 0, parent: null);
        }
        catch (ArgumentException exception)
        {
            // A JSON object with a repeated member name cannot be resolved unambiguously. A disclosed value is
            // judged where it is resolved, so a repeat reaching here is in the issuer-signed payload itself.
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT payload repeats a member name.",
                exception
            );
        }

        if (Disclosed.Count != count)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.UnreferencedDisclosure,
                "A presented disclosure is referenced by no digest."
            );

        return new ProcessedSdJwtPayload(claims, Disclosed);
    }

    private static JsonObject ToObject(JsonElement payload) =>
        JsonObject.Create(payload)
        ?? throw new SdJwtException(
            SdJwtVerificationFailureCodes.InvalidSdJwt,
            "The issuer-signed JWT payload must be a JSON object."
        );

    private void ProcessObject(JsonObject obj, string path, int depth, Disclosure? parent)
    {
        EnsureDepth(depth);

        List<string>? digests = null;
        var names = new List<string>(obj.Count);
        foreach (var member in obj)
            names.Add(member.Key);

        foreach (var name in names)
        {
            var node = obj[name];
            switch (name)
            {
                case JoseClaimNames.Payload.Sd:
                    EnsureNotProtected(path);
                    digests = ReadDigestArray(node);
                    obj.Remove(name);
                    break;

                case JoseClaimNames.Payload.SdAlg:
                    if (depth != 0)
                        throw new SdJwtException(
                            SdJwtVerificationFailureCodes.ReservedClaimName,
                            "The '_sd_alg' claim is permitted only at the top level of the issuer-signed JWT payload."
                        );
                    obj.Remove(name);
                    break;

                case JoseClaimNames.Payload.SdArrayElement:
                    throw new SdJwtException(
                        SdJwtVerificationFailureCodes.ReservedClaimName,
                        "The '...' claim name is permitted only as the one member of an array element placeholder."
                    );

                default:
                    // A scalar holds no digest, so it needs no path.
                    if (node is JsonObject or JsonArray)
                        ProcessNode(node, JsonPointer.AppendMember(path, name), depth + 1, parent);
                    break;
            }
        }

        if (digests is null)
            return;

        foreach (var digest in digests)
        {
            if (!TryTakeDisclosure(digest, out var disclosure))
                continue;

            if (disclosure.IsArrayElement)
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.DisclosureMismatch,
                    "A digest of an object property references an array element disclosure."
                );

            // The name is the holder's input, unbounded, so the reason names the rule and not the name.
            var claimName = disclosure.ClaimName;
            if (obj.ContainsKey(claimName))
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.DuplicateClaim,
                    "A disclosed claim is already present in the object that discloses it."
                );

            if (depth == 0 && SdJwtClaimRules.IsProtected(claimName, Profile))
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.ProtectedClaimDisclosed,
                    SdJwtClaimRules.DescribeProtected(claimName, Profile)
                );

            var claimPath = JsonPointer.AppendMember(path, claimName);
            var valueNode = ToNode(disclosure.Value);
            obj[claimName] = valueNode;
            Disclosed.Add(new DisclosedClaim(disclosure, claimPath, parent));

            ProcessDisclosedValue(valueNode, claimPath, depth + 1, disclosure);
        }
    }

    private void ProcessArray(JsonArray array, string path, int depth, Disclosure? parent)
    {
        EnsureDepth(depth);

        // The resolved elements are collected in one pass and written back once, so a run of decoys costs no more
        // than a run of elements; an element's index is its index once the decoys before it are dropped.
        var count = array.Count;
        var kept = new List<JsonNode?>(count);
        var hasPlaceholder = false;
        for (var i = 0; i < count; i++)
        {
            var element = array[i];
            if (
                element is not JsonObject placeholder
                || !placeholder.ContainsKey(JoseClaimNames.Payload.SdArrayElement)
            )
            {
                if (element is JsonObject or JsonArray)
                    ProcessNode(
                        element,
                        JsonPointer.AppendIndex(path, kept.Count),
                        depth + 1,
                        parent
                    );
                kept.Add(element);
                continue;
            }

            hasPlaceholder = true;
            EnsureNotProtected(path);

            if (placeholder.Count != 1)
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.InvalidDigestStructure,
                    "An array element placeholder must be an object whose one member is '...'."
                );

            var digest = ReadDigest(placeholder[JoseClaimNames.Payload.SdArrayElement]);
            if (!TryTakeDisclosure(digest, out var disclosure))
            {
                // A decoy digest stands in for no element.
                continue;
            }

            if (!disclosure.IsArrayElement)
                throw new SdJwtException(
                    SdJwtVerificationFailureCodes.DisclosureMismatch,
                    "A digest of an array element references an object property disclosure."
                );

            var elementPath = JsonPointer.AppendIndex(path, kept.Count);
            var valueNode = ToNode(disclosure.Value);
            kept.Add(valueNode);
            Disclosed.Add(new DisclosedClaim(disclosure, elementPath, parent));

            ProcessDisclosedValue(valueNode, elementPath, depth + 1, disclosure);
        }

        if (!hasPlaceholder)
            return;

        array.Clear();
        foreach (var node in kept)
            array.Add(node);
    }

    // A disclosed object is materialized as it is resolved, so a repeated member name in a disclosure is the
    // holder's error, reported as such, and never read as a repeat in the issuer-signed payload.
    private void ProcessDisclosedValue(
        JsonNode? node,
        string path,
        int depth,
        Disclosure disclosure
    )
    {
        try
        {
            ProcessNode(node, path, depth, disclosure);
        }
        catch (ArgumentException exception)
        {
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidDisclosure,
                "A disclosed value repeats a member name.",
                exception
            );
        }
    }

    private void ProcessNode(JsonNode? node, string path, int depth, Disclosure? parent)
    {
        switch (node)
        {
            case JsonObject obj:
                ProcessObject(obj, path, depth, parent);
                break;

            case JsonArray array:
                ProcessArray(array, path, depth, parent);
                break;
        }
    }

    private bool TryTakeDisclosure(string digest, [NotNullWhen(true)] out Disclosure? disclosure)
    {
        if (!SeenDigests.Add(digest))
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.DuplicateDigest,
                "A digest appears more than once in the SD-JWT."
            );

        return DisclosuresByDigest.TryGetValue(digest, out disclosure);
    }

    // A digest beneath a protected top-level claim would let the holder withhold part of it unnoticed, so none is
    // resolved there, decoy or not. The root carries no top-level name, so its own digests are judged by the claim
    // name each discloses.
    private void EnsureNotProtected(string path)
    {
        if (path.Length == 0)
            return;

        var claimName = JsonPointer.GetFirstToken(path);
        if (SdJwtClaimRules.IsProtected(claimName, Profile))
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.ProtectedClaimDisclosed,
                SdJwtClaimRules.DescribeProtected(claimName, Profile)
            );
    }

    private void EnsureDepth(int depth)
    {
        if (depth > MaxNestingDepth)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.NestingLimitExceeded,
                $"The claims nest deeper than the limit of {MaxNestingDepth}."
            );
    }

    private static List<string> ReadDigestArray(JsonNode? node)
    {
        if (node is not JsonArray array)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidDigestStructure,
                "The '_sd' claim must be an array of strings."
            );

        var digests = new List<string>(array.Count);
        foreach (var element in array)
            digests.Add(ReadDigest(element));

        return digests;
    }

    private static string ReadDigest(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var digest))
            return digest;

        throw new SdJwtException(
            SdJwtVerificationFailureCodes.InvalidDigestStructure,
            "A digest must be a string."
        );
    }

    private static JsonNode? ToNode(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonObject.Create(value),
            JsonValueKind.Array => JsonArray.Create(value),
            _ => JsonValue.Create(value),
        };
}
