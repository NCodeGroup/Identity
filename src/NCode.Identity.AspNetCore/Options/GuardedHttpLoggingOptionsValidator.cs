// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;

namespace NCode.Identity.AspNetCore.Options;

// See ADR-1503.
/// <summary>
/// Validates <see cref="HttpLoggingOptions"/>, so a body, query string or trailer field, or an allow-listed header
/// whose value carries a credential, stops the host when it starts instead of writing a credential to the log.
/// </summary>
internal sealed class GuardedHttpLoggingOptionsValidator : IValidateOptions<HttpLoggingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HttpLoggingOptions options)
    {
        var failures = new List<string>();

        var unpermitted = options.LoggingFields & ~GuardedHttpLoggingDefaults.PermittedFields;
        if (unpermitted != HttpLoggingFields.None)
        {
            failures.Add(
                $"HttpLoggingOptions.LoggingFields enables '{unpermitted}', which can write credentials or personal data to the log; an endpoint opts in to its own request or response body with WithRequestBodyLogging() or WithResponseBodyLogging()."
            );
        }

        var requestHeaderFailures = FindCredentialHeaders(
            options.RequestHeaders,
            GuardedHttpLoggingDefaults.CredentialRequestHeaders,
            nameof(options.RequestHeaders)
        );
        failures.AddRange(requestHeaderFailures);

        var responseHeaderFailures = FindCredentialHeaders(
            options.ResponseHeaders,
            GuardedHttpLoggingDefaults.CredentialResponseHeaders,
            nameof(options.ResponseHeaders)
        );
        failures.AddRange(responseHeaderFailures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> FindCredentialHeaders(
        ISet<string> allowList,
        string[] credentialHeaders,
        string optionName
    )
    {
        foreach (var header in credentialHeaders)
        {
            if (allowList.Contains(header, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"HttpLoggingOptions.{optionName} lists '{header}', whose value carries a credential; remove it so HTTP logging writes it as [Redacted].";
            }
        }
    }
}
