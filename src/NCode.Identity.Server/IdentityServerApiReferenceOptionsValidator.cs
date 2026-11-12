// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Server;

/// <summary>
/// Validates <see cref="IdentityServerApiReferenceOptions"/>: the API reference page renders the OpenAPI document this
/// host serves, so enabling the page without the document is a configuration that cannot work.
/// </summary>
// See ADR-1481.
internal sealed class IdentityServerApiReferenceOptionsValidator
    : IValidateOptions<IdentityServerApiReferenceOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IdentityServerApiReferenceOptions options)
    {
        if (!options.PageEnabled || options.DocumentEnabled)
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"{IdentityServerApiReferenceOptions.DefaultSectionName}:{nameof(options.PageEnabled)} requires "
                + $"{IdentityServerApiReferenceOptions.DefaultSectionName}:{nameof(options.DocumentEnabled)}: "
                + "the API reference page renders the OpenAPI document this host serves; "
                + $"{nameof(options.PageEnabled)} is true and {nameof(options.DocumentEnabled)} is false."
        );
    }
}
