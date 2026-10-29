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

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures how the BFF keeps the access token of a session fresh.
/// </summary>
[PublicAPI]
public sealed class BffTokenOptions
{
    /// <summary>
    /// Gets or sets how long before it expires an access token is refreshed. The default is 60 seconds. The
    /// configuration key is <c>Bff:Tokens:RefreshBeforeExpiry</c>.
    /// </summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets how long the cluster-wide refresh lease lasts. The default is 40 seconds. The configuration key
    /// is <c>Bff:Tokens:RefreshLeaseDuration</c>.
    /// </summary>
    public TimeSpan RefreshLeaseDuration { get; set; } = TimeSpan.FromSeconds(40);
}
