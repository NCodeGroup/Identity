// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Validates that <see cref="BffOptions.Frontends"/> configures the frontend that serves the management console, so a
/// host without it stops at startup instead of failing on the first page request.
/// </summary>
internal sealed class ManagementConsoleFrontendValidator : IValidateOptions<BffOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return
            options.Frontends is not null
            && options.Frontends.ContainsKey(ManagementConsoleDefaults.FrontendName)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Bff:Frontends must configure the '{ManagementConsoleDefaults.FrontendName}' frontend that serves the management console."
            );
    }
}
