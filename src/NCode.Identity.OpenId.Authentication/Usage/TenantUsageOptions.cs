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

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Contains the options for recording tenant usage into the durable daily ledger (ADR-0199).
/// </summary>
public sealed class TenantUsageOptions
{
    /// <summary>
    /// Gets or sets how often each replica adds the usage it recorded to the ledger. A replica that stops abruptly loses
    /// at most this much of its usage. The default is one minute.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long the ledger keeps a day's usage. The default is 400 days: a year of history to bill and
    /// report from, and a month more to settle a dispute over the oldest bill.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(400);
}
