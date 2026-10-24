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

using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.Options;

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Validates the in-process dispatcher's options when the host starts, so a queue that holds no job, a worker that
/// runs no job at once or a full-queue behavior the channel does not define stops the host instead of silently
/// dropping or never running the dispatched jobs.
/// </summary>
// See ADR-0232 and ADR-0242.
internal sealed class InProcessJobDispatcherOptionsValidator
    : IValidateOptions<InProcessJobDispatcherOptions>
{
    private static string FullModeNames { get; } =
        string.Join(", ", Enum.GetNames<BoundedChannelFullMode>());

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, InProcessJobDispatcherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.QueueCapacity < 1)
        {
            failures.Add(
                $"{nameof(InProcessJobDispatcherOptions)}.{nameof(InProcessJobDispatcherOptions.QueueCapacity)} ('{InProcessJobDispatcherOptions.SectionName}:{nameof(InProcessJobDispatcherOptions.QueueCapacity)}') must be at least 1; it is {options.QueueCapacity}."
            );
        }

        if (options.MaxConcurrentJobs < 1)
        {
            failures.Add(
                $"{nameof(InProcessJobDispatcherOptions)}.{nameof(InProcessJobDispatcherOptions.MaxConcurrentJobs)} ('{InProcessJobDispatcherOptions.SectionName}:{nameof(InProcessJobDispatcherOptions.MaxConcurrentJobs)}') must be at least 1; it is {options.MaxConcurrentJobs}."
            );
        }

        if (!Enum.IsDefined(options.FullMode))
        {
            failures.Add(
                $"{nameof(InProcessJobDispatcherOptions)}.{nameof(InProcessJobDispatcherOptions.FullMode)} ('{InProcessJobDispatcherOptions.SectionName}:{nameof(InProcessJobDispatcherOptions.FullMode)}') must be one of {FullModeNames}; it is {options.FullMode}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
