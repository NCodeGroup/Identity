// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.Options;

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Validates the in-process dispatcher's options when the host starts, so a queue that holds no job, a worker that
/// runs no job at once or a full-queue behavior the channel does not define stops the host instead of silently
/// dropping or never running the dispatched jobs.
/// </summary>
// See ADR-1232 and ADR-1242.
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
