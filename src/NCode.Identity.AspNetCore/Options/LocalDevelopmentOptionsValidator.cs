// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NCode.Identity.AspNetCore.Options;

/// <summary>
/// Validates <see cref="LocalDevelopmentOptions"/>, so a leaked local inner-loop switch stops a Production host when it
/// starts instead of relaxing it.
/// </summary>
internal sealed class LocalDevelopmentOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<LocalDevelopmentOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, LocalDevelopmentOptions options)
    {
        if (options.Enabled && environment.IsEnvironment(Environments.Production))
        {
            return ValidateOptionsResult.Fail(
                "LocalDevelopment:Enabled is a local inner-loop switch and the Production environment refuses it; remove it from the deployment's configuration."
            );
        }

        return ValidateOptionsResult.Success;
    }
}
