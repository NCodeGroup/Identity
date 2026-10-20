#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.Options;

namespace NCode.Identity.Server;

/// <summary>
/// Validates <see cref="BootstrapAdminOptions"/> when the bootstrap administrator is configured: it must name the one
/// tenant it is seeded into. Without that, every tenant — including each one created at runtime — would receive a client
/// whose tokens carry the <c>GlobalAdmin</c> role.
/// </summary>
// See ADR-0137 (fail at startup) and ADR-0166 (a seeder that names a tenant seeds exactly that tenant).
internal sealed class BootstrapAdminOptionsValidator : IValidateOptions<BootstrapAdminOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BootstrapAdminOptions options)
    {
        if (!options.IsEnabled || !string.IsNullOrEmpty(options.TenantId))
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"{BootstrapAdminOptions.DefaultSectionName}:{nameof(options.TenantId)} is required when the bootstrap "
                + "administrator is configured: name the one tenant its client is seeded into."
        );
    }
}
