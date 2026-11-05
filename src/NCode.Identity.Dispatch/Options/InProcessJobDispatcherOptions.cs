// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;
using JetBrains.Annotations;

namespace NCode.Identity.Dispatch.Options;

/// <summary>
/// Configures the built-in in-process job dispatcher's bounded queue and how many of its jobs run at once. These are
/// the knobs a host tunes for its throughput and back-pressure needs; a deployment that needs durable or distributed
/// dispatch replaces the adapter entirely (register a custom <see cref="IJobDispatcher"/>) rather than tuning these.
/// </summary>
[PublicAPI]
public sealed class InProcessJobDispatcherOptions
{
    /// <summary>
    /// Gets the name of the configuration section that binds these options; <c>AddIdentityServer</c> binds it beneath
    /// its OpenID section.
    /// </summary>
    public const string SectionName = "InProcessJobDispatcher";

    /// <summary>
    /// Gets or sets the maximum number of pending jobs the in-memory queue buffers before <see cref="FullMode"/>
    /// applies. It must be at least <c>1</c>. The default is <c>1024</c>.
    /// </summary>
    public int QueueCapacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the most jobs the in-process dispatcher runs at once. A job that waits on a slow endpoint holds
    /// one of them while the others run the jobs queued behind it. It must be at least <c>1</c>; the default is
    /// <c>16</c>.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 16;

    /// <summary>
    /// Gets or sets the behavior when a job is dispatched while the queue is at <see cref="QueueCapacity"/>. The
    /// default is <see cref="BoundedChannelFullMode.Wait"/>, under which a full queue reports the dispatch as dropped
    /// (logged) rather than blocking the dispatching path; a durable dispatcher adapter is the answer to guaranteed
    /// delivery. It must be a member of <see cref="BoundedChannelFullMode"/>, which configuration names, for example
    /// <c>DropOldest</c>.
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;
}
