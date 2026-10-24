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
using Microsoft.AspNetCore.Http;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class DefaultRateLimitDescriptorDataSourceTests : BaseTests
{
    private const string ClientId = "client-1";

    private DefaultRateLimitDescriptorDataSource CreateDataSource() =>
        new(CreateStrictMock<INullChangeToken>().Object);

    private RateLimitDescriptor GetDescriptor(string name) =>
        Assert.Single(CreateDataSource().Collection, descriptor => descriptor.Name == name);

    private RateLimitContext CreateAddressContext(string? remoteAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteAddress is null
            ? null
            : IPAddress.Parse(remoteAddress);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Http).Returns(httpContext).Verifiable();
        return new RateLimitContext { OpenIdContext = mockContext.Object };
    }

    private RateLimitContext CreateClientContext(OpenIdClient? client) =>
        new() { OpenIdContext = CreateStrictMock<OpenIdContext>().Object, Client = client };

    private static void AssertDescriptor(
        RateLimitDescriptor actual,
        string stage,
        int order,
        TimeSpan window,
        bool isSliding,
        SettingKey<int> limitKey,
        SettingKey<int>? overageKey,
        bool isMeteredOnly,
        string? usageMeter,
        string? overageUsageMeter
    )
    {
        Assert.Equal(stage, actual.Stage);
        Assert.Equal(order, actual.Order);
        Assert.Equal(window, actual.Window);
        Assert.Equal(isSliding, actual.IsSliding);
        Assert.Equal(limitKey, actual.LimitKey);
        Assert.Equal(overageKey, actual.OverageKey);
        Assert.Equal(isMeteredOnly, actual.IsMeteredOnly);
        Assert.Equal(usageMeter, actual.UsageMeter);
        Assert.Equal(overageUsageMeter, actual.OverageUsageMeter);
    }

    #region Collection Property Tests

    [Fact]
    public void Collection_DescribesTheBuiltInLimits()
    {
        var dataSource = CreateDataSource();

        var descriptors = dataSource.Collection.ToList();

        Assert.Equal(
            [
                RateLimitPartitions.Address,
                RateLimitPartitions.Tenant,
                RateLimitPartitions.DailyRequests,
                RateLimitPartitions.Client,
                RateLimitPartitions.DailyTokenRequests,
            ],
            descriptors.Select(descriptor => descriptor.Name)
        );
        AssertDescriptor(
            descriptors[0],
            stage: RateLimitStages.Request,
            order: 100,
            window: TimeSpan.FromMinutes(1),
            isSliding: true,
            limitKey: OpenIdSettingKeys.RateLimitAddressPerMinute,
            overageKey: null,
            isMeteredOnly: false,
            usageMeter: null,
            overageUsageMeter: null
        );
        AssertDescriptor(
            descriptors[1],
            stage: RateLimitStages.Request,
            order: 200,
            window: TimeSpan.FromMinutes(1),
            isSliding: true,
            limitKey: OpenIdSettingKeys.RateLimitTenantPerMinute,
            overageKey: null,
            isMeteredOnly: false,
            usageMeter: null,
            overageUsageMeter: null
        );
        AssertDescriptor(
            descriptors[2],
            stage: RateLimitStages.Request,
            order: 300,
            window: TimeSpan.FromDays(1),
            isSliding: false,
            limitKey: OpenIdSettingKeys.QuotaTenantRequestsPerDay,
            overageKey: OpenIdSettingKeys.QuotaTenantRequestsOveragePerDay,
            isMeteredOnly: true,
            usageMeter: TenantUsageMeters.Requests,
            overageUsageMeter: TenantUsageMeters.RequestsOverage
        );
        AssertDescriptor(
            descriptors[3],
            stage: RateLimitStages.ClientAuthentication,
            order: 100,
            window: TimeSpan.FromMinutes(1),
            isSliding: true,
            limitKey: OpenIdSettingKeys.RateLimitClientPerMinute,
            overageKey: null,
            isMeteredOnly: false,
            usageMeter: null,
            overageUsageMeter: null
        );
        AssertDescriptor(
            descriptors[4],
            stage: RateLimitStages.TokenRequest,
            order: 100,
            window: TimeSpan.FromDays(1),
            isSliding: false,
            limitKey: OpenIdSettingKeys.QuotaTenantTokenRequestsPerDay,
            overageKey: OpenIdSettingKeys.QuotaTenantTokenRequestsOveragePerDay,
            isMeteredOnly: false,
            usageMeter: TenantUsageMeters.TokenRequests,
            overageUsageMeter: TenantUsageMeters.TokenRequestsOverage
        );
        Assert.Equal(string.Empty, descriptors[1].PartitionValueSelector(default));
        Assert.Equal(string.Empty, descriptors[2].PartitionValueSelector(default));
        Assert.Equal(string.Empty, descriptors[4].PartitionValueSelector(default));
    }

    [Fact]
    public void Collection_AddressSelector_CountsAnIPv6AddressByItsSlash64()
    {
        var descriptor = GetDescriptor(RateLimitPartitions.Address);
        var context = CreateAddressContext("2001:db8:1:2:3:4:5:6");

        var actual = descriptor.PartitionValueSelector(context);

        Assert.Equal("2001:db8:1:2::/64", actual);
    }

    [Fact]
    public void Collection_AddressSelector_CountsAnIPv4MappedAddressAsIPv4()
    {
        var descriptor = GetDescriptor(RateLimitPartitions.Address);
        var context = CreateAddressContext("::ffff:192.0.2.1");

        var actual = descriptor.PartitionValueSelector(context);

        Assert.Equal("192.0.2.1", actual);
    }

    [Fact]
    public void Collection_AddressSelector_WithoutARemoteAddress_ReturnsNull()
    {
        var descriptor = GetDescriptor(RateLimitPartitions.Address);
        var context = CreateAddressContext(remoteAddress: null);

        var actual = descriptor.PartitionValueSelector(context);

        Assert.Null(actual);
    }

    [Fact]
    public void Collection_ClientSelector_ReturnsTheClientId()
    {
        var descriptor = GetDescriptor(RateLimitPartitions.Client);
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.ClientId).Returns(ClientId).Verifiable();
        var context = CreateClientContext(mockClient.Object);

        var actual = descriptor.PartitionValueSelector(context);

        Assert.Equal(ClientId, actual);
    }

    [Fact]
    public void Collection_ClientSelector_WithoutAClient_ReturnsNull()
    {
        var descriptor = GetDescriptor(RateLimitPartitions.Client);
        var context = CreateClientContext(client: null);

        var actual = descriptor.PartitionValueSelector(context);

        Assert.Null(actual);
    }

    #endregion
}
