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

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class DefaultRateLimitDescriptorCatalogTests : BaseTests
{
    private const string Stage = "test";

    private Mock<ISettingDescriptorCollection> MockSettingDescriptors { get; }

    public DefaultRateLimitDescriptorCatalogTests()
    {
        MockSettingDescriptors = CreateStrictMock<ISettingDescriptorCollection>();
    }

    #region Scaffolding

    private DefaultRateLimitDescriptorCatalog CreateCatalog(
        params IEnumerable<RateLimitDescriptor>[] dataSources
    )
    {
        var mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(MockSettingDescriptors.Object).Verifiable();

        return new DefaultRateLimitDescriptorCatalog(
            [.. dataSources.Select(items => new StaticDataSource<RateLimitDescriptor>(items))],
            mockProvider.Object
        );
    }

    private void RegisterSetting(SettingKey<int> key, bool isRegistered = true)
    {
        SettingDescriptor<int>? descriptor = isRegistered
            ? new SettingDescriptor<int>
            {
                Name = key.SettingName,
                AppliesTo = [OpenIdSettingScopes.Tenant],
            }
            : null;

        MockSettingDescriptors
            .Setup(x => x.TryGet(key, out descriptor))
            .Returns(isRegistered)
            .Verifiable();
    }

    private static SettingKey<int> GetLimitKey(string name) => new($"{name}_limit");

    private static SettingKey<int> GetOverageKey(string name) => new($"{name}_overage");

    private static RateLimitDescriptor CreateDescriptor(
        string name,
        string stage = Stage,
        int order = 0,
        TimeSpan? window = null,
        bool isSliding = false,
        SettingKey<int>? overageKey = null,
        string? usageMeter = null,
        string? overageUsageMeter = null
    ) =>
        new()
        {
            Name = name,
            Stage = stage,
            Order = order,
            Window = window ?? TimeSpan.FromMinutes(1),
            IsSliding = isSliding,
            LimitKey = GetLimitKey(name),
            OverageKey = overageKey,
            UsageMeter = usageMeter,
            OverageUsageMeter = overageUsageMeter,
            PartitionValueSelector = static _ => string.Empty,
        };

    #endregion

    #region GetStage Tests

    [Fact]
    public void GetStage_ReturnsTheStageDescriptorsByOrderThenName()
    {
        var descriptors = new[]
        {
            CreateDescriptor("b", order: 200),
            CreateDescriptor("c", order: 100),
            CreateDescriptor("other", stage: "other_stage", order: 50),
            CreateDescriptor("a", order: 200),
            CreateDescriptor("B", order: 200),
        };
        foreach (var descriptor in descriptors)
            RegisterSetting(descriptor.LimitKey);

        var catalog = CreateCatalog(descriptors[..2], descriptors[2..]);

        var actual = catalog.GetStage(Stage);

        Assert.Equal(["c", "B", "a", "b"], actual.Select(descriptor => descriptor.Name));
    }

    [Fact]
    public void GetStage_WhenNothingIsRegisteredForTheStage_ReturnsEmpty()
    {
        var descriptor = CreateDescriptor("other", stage: "other_stage");
        RegisterSetting(descriptor.LimitKey);
        var catalog = CreateCatalog([descriptor]);

        var actual = catalog.GetStage(Stage);

        Assert.True(actual.IsEmpty);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTwoDescriptorsShareAName_Throws()
    {
        var first = CreateDescriptor("shared");
        RegisterSetting(first.LimitKey);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([first], [CreateDescriptor("shared", order: 1)])
        );

        Assert.Contains("'shared'", exception.Message);
    }

    [Fact]
    public void Constructor_WhenTheLimitSettingIsNotRegistered_Throws()
    {
        var descriptor = CreateDescriptor("unregistered");
        RegisterSetting(descriptor.LimitKey, isRegistered: false);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCatalog([descriptor]));

        Assert.Contains("'unregistered'", exception.Message);
    }

    [Fact]
    public void Constructor_WhenASlidingWindowHasAnOverage_Throws()
    {
        var overageKey = GetOverageKey("sliding");
        var descriptor = CreateDescriptor("sliding", isSliding: true, overageKey: overageKey);
        RegisterSetting(descriptor.LimitKey);
        RegisterSetting(overageKey);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCatalog([descriptor]));

        Assert.Contains("'sliding'", exception.Message);
    }

    [Fact]
    public void Constructor_WhenAnOverageMeterHasNoOverageSetting_Throws()
    {
        var descriptor = CreateDescriptor("meter", overageUsageMeter: "meter_overage");
        RegisterSetting(descriptor.LimitKey);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCatalog([descriptor]));

        Assert.Contains("'meter'", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void Constructor_WhenTheWindowIsNotPositive_Throws(int seconds)
    {
        var descriptor = CreateDescriptor("window", window: TimeSpan.FromSeconds(seconds));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCatalog([descriptor]));

        Assert.Contains("'window'", exception.Message);
    }

    [Fact]
    public void Constructor_WhenAUsageMeterIsTooLong_Throws()
    {
        var meter = new string('m', OpenIdMaxLengths.UsageMeter + 1);
        var descriptor = CreateDescriptor("long", usageMeter: meter);
        RegisterSetting(descriptor.LimitKey);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCatalog([descriptor]));

        Assert.Contains("'long'", exception.Message);
    }

    #endregion

    private sealed class StaticDataSource<T>(IEnumerable<T> items) : ICollectionDataSource<T>
    {
        public IEnumerable<T> Collection { get; } = [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}
