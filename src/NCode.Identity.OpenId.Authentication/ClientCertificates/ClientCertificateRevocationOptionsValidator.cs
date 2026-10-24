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
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Validates the options for retrieving and caching certificate revocation lists when the host starts, so a timeout
/// that a timer refuses or a limit that is not positive stops the host instead of failing the first time a list is
/// retrieved.
/// </summary>
// See ADR-0252.
internal sealed class ClientCertificateRevocationOptionsValidator
    : IValidateOptions<ClientCertificateRevocationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ClientCertificateRevocationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!TimerPeriod.IsValid(options.FetchTimeout))
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.FetchTimeout)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.FetchTimeout)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.FetchTimeout}."
            );
        }

        if (options.MaxResponseSizeBytes < 1)
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.MaxResponseSizeBytes)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.MaxResponseSizeBytes)}') must be at least 1; it is {options.MaxResponseSizeBytes}."
            );
        }

        if (options.MaxCacheDuration <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.MaxCacheDuration)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.MaxCacheDuration)}') must be positive; it is {options.MaxCacheDuration}."
            );
        }

        if (options.FailureCacheDuration <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.FailureCacheDuration)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.FailureCacheDuration)}') must be positive; it is {options.FailureCacheDuration}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
