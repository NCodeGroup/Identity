// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Captures the instant at which the current process started its setup, so that a generated setup token stored by an
/// earlier process can be told from one issued by this process or by a replica that started with it.
/// </summary>
/// <param name="timeProvider">The <see cref="TimeProvider"/> that supplies the current instant.</param>
internal sealed class SetupProcessClock(TimeProvider timeProvider)
{
    /// <summary>
    /// Gets the instant at which this clock was created, which is when the process started.
    /// </summary>
    public DateTimeOffset StartedAt { get; } = timeProvider.GetUtcNow();
}
