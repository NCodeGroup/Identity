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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Resolves the merged settings of the request's tenant from the ambient OpenID request environment, which every HTTP
/// surface materializes (ADR-0036).
/// </summary>
internal static class AmbientTenantSettings
{
    public static IReadOnlySettingCollection GetRequired(IHttpContextAccessor httpContextAccessor)
    {
        var openIdContext =
            httpContextAccessor.HttpContext?.Features.Get<IOpenIdContextFeature>()?.OpenIdContext
            ?? throw new InvalidOperationException(
                "The OpenID request environment is not available; principal resolution must run within a materialized OpenID request."
            );

        return openIdContext.Tenant.SettingsProvider.Collection;
    }
}
