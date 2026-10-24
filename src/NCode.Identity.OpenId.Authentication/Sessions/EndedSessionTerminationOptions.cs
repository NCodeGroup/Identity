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

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Contains the options for terminating the sessions that ended while no request used them.
/// </summary>
public sealed class EndedSessionTerminationOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "EndedSessionTermination";

    /// <summary>
    /// Gets or sets how often one replica terminates the sessions that ended while no request used them, which
    /// revokes the grants issued under each and notifies its relying parties. The default is one minute. It is at
    /// least one millisecond and at most about 49.7 days, the periods a timer accepts.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
}
