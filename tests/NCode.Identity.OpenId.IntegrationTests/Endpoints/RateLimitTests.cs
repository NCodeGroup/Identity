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

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Drives a tenant past its <c>rate_limit_tenant_per_minute</c> limit through the real host and confirms the refused
/// request is answered <c>429 Too Many Requests</c> with <c>Retry-After</c> (ADR-0196).
/// </summary>
public class RateLimitTests
{
    [Fact]
    public async Task Discovery_WhenTheTenantIsOverItsRateLimit_ReturnsTooManyRequests()
    {
        // The server's settings cascade into the tenant, so a server-wide limit of two applies to the static tenant.
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                [$"OpenId:Server:Settings:{OpenIdSettingNames.RateLimitTenantPerMinute}"] = "2",
            }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The third request is over the limit whether or not the minute rolls over between the requests: the previous
        // window's count weighs almost fully at the start of the next one.
        for (var i = 0; i < 2; ++i)
        {
            using var admitted = await client.GetAsync("/.well-known/openid-configuration");
            Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        }

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.TemporarilyUnavailable,
            document.RootElement.GetProperty("error").GetString()
        );
    }
}
