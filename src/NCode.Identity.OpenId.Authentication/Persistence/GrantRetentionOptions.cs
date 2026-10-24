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

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Contains the options for removing the grants that ended from the store.
/// </summary>
public sealed class GrantRetentionOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "GrantRetention";

    /// <summary>
    /// Gets or sets how long a grant is kept after it ends, that is after it expires or, if earlier, after it is
    /// revoked. A refresh token presented again within this time is still recognized as a reuse, and a replayed
    /// authorization code still revokes its session. The default is 14 days.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Gets or sets how often each replica removes the grants that ended before the retention. The default is one
    /// hour. It is at least one millisecond and at most about 49.7 days, the periods a timer accepts.
    /// </summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromHours(1);
}
