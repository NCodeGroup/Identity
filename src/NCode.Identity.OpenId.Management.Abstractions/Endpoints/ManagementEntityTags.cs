// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Holds the rules that every management route follows to send a concurrency token as an RFC 9110 entity tag and
/// to compare the <c>If-Match</c> and <c>If-None-Match</c> request headers with it.
/// </summary>
[PublicAPI]
public static class ManagementEntityTags
{
    private const string MismatchDetailPrefix =
        "The supplied If-Match entity tag does not match the current ";

    private const string MalformedDetail =
        "The If-Match header must be \"*\", a list of entity tags such as \"\\\"<concurrency token>\\\"\", or the resource's concurrencyToken.";

    private const string WeakPrefix = "W/";

    /// <summary>
    /// Formats a concurrency token as a strong entity tag, which is the token between double quotes.
    /// </summary>
    /// <param name="concurrencyToken">The concurrency token of the resource.</param>
    /// <returns>The strong entity tag, such as <c>"abc"</c> with its double quotes.</returns>
    /// <exception cref="FormatException">
    /// <paramref name="concurrencyToken"/> is not valid content for an entity tag.
    /// </exception>
    public static string Format(string concurrencyToken) =>
        new EntityTagHeaderValue($"\"{concurrencyToken}\"").Tag.ToString();

    /// <summary>
    /// Determines whether an <c>If-None-Match</c> header names the current entity tag of a resource. The comparison
    /// is weak, as RFC 9110 requires for <c>If-None-Match</c>, so a weak tag with the same value matches.
    /// </summary>
    /// <param name="ifNoneMatch">The value of the <c>If-None-Match</c> request header.</param>
    /// <param name="concurrencyToken">The current concurrency token of the resource.</param>
    /// <returns>
    /// <see langword="true"/> when the header is <c>*</c> or lists the current entity tag; <see langword="false"/>
    /// when the header is missing, empty, malformed or lists no matching tag.
    /// </returns>
    public static bool MatchesIfNoneMatch(string? ifNoneMatch, string concurrencyToken)
    {
        if (string.IsNullOrEmpty(ifNoneMatch))
        {
            return false;
        }

        if (!EntityTagHeaderValue.TryParseList([ifNoneMatch], out var tags))
        {
            return false;
        }

        var current = new EntityTagHeaderValue(Format(concurrencyToken));
        return Matches(tags, current, useStrongComparison: false);
    }

    /// <summary>
    /// Compares an <c>If-Match</c> header with the current entity tag of a resource. The header is accepted when it
    /// is absent or empty, is <c>*</c>, lists a strong entity tag that equals the current one under strong
    /// comparison, or is a lone bare concurrency token that equals the current one. A weak entity tag never matches.
    /// </summary>
    /// <param name="ifMatch">The value of the <c>If-Match</c> request header.</param>
    /// <param name="concurrencyToken">The current concurrency token of the resource.</param>
    /// <param name="resource">
    /// The resource whose concurrency token the header is compared with, such as <c>settings</c> or <c>secret</c>,
    /// which the detail of a mismatch names.
    /// </param>
    /// <returns>
    /// <see langword="null"/> when the header is accepted; otherwise a <c>412 Precondition Failed</c> error whose
    /// detail names the resource for a mismatch, or the accepted forms for a malformed header.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="resource"/> is <see langword="null"/>, empty or white space.
    /// </exception>
    public static ManagementError? GetIfMatchErrorOrDefault(
        string? ifMatch,
        string concurrencyToken,
        string resource
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        if (string.IsNullOrEmpty(ifMatch))
        {
            return null;
        }

        if (IsBareToken(ifMatch))
        {
            return string.Equals(ifMatch, concurrencyToken, StringComparison.Ordinal)
                ? null
                : CreateMismatchError(resource);
        }

        if (!EntityTagHeaderValue.TryParseList([ifMatch], out var tags))
        {
            return CreateError(MalformedDetail);
        }

        var current = new EntityTagHeaderValue(Format(concurrencyToken));
        return Matches(tags, current, useStrongComparison: true)
            ? null
            : CreateMismatchError(resource);
    }

    private static bool IsBareToken(string value) =>
        !string.Equals(value, EntityTagHeaderValue.Any.Tag.ToString(), StringComparison.Ordinal)
        && !value.StartsWith(WeakPrefix, StringComparison.Ordinal)
        && !value.Any(character => character is '"' or ',' || char.IsWhiteSpace(character));

    private static bool Matches(
        IList<EntityTagHeaderValue> tags,
        EntityTagHeaderValue current,
        bool useStrongComparison
    ) =>
        tags.Any(tag =>
            tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(current, useStrongComparison)
        );

    private static ManagementError CreateMismatchError(string resource) =>
        CreateError($"{MismatchDetailPrefix}{resource}.");

    private static ManagementError CreateError(string detail) =>
        new() { StatusCode = StatusCodes.Status412PreconditionFailed, Detail = detail };
}
