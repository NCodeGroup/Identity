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

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the <c>staging</c> tenant environment type: it mirrors production's rules by selecting the
/// <c>production</c> security profile, while still showing the non-production banner.
/// </summary>
// See ADR-0287.
internal sealed class StagingTenantEnvironmentType : ITenantEnvironmentType
{
    /// <inheritdoc />
    public string TypeName => OpenIdConstants.TenantEnvironmentTypes.Staging;

    /// <inheritdoc />
    public bool IsProduction => false;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SecurityProfiles { get; } =
    [OpenIdConstants.SecurityProfiles.Production];
}
