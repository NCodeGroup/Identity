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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class RootSettingsCollectionDataSourceTests : BaseTests
{
    private const string SectionPath = "OpenId:Server:Settings";

    private static IChangeToken NeverChanges { get; } =
        new CancellationChangeToken(CancellationToken.None);

    private int CreatedCollectionCount { get; set; }

    private RootSettingsCollectionDataSource CreateDataSource(
        IConfigurationRoot configuration,
        ILogger<RootSettingsCollectionDataSource> logger
    )
    {
        var mockCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        mockCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<IReadOnlyCollection<Setting>>(),
                    It.IsAny<IReadOnlyCollection<Setting>>()
                )
            )
            .Returns(
                (IReadOnlyCollection<Setting> settings, IReadOnlyCollection<Setting> _) => settings
            )
            .Verifiable();

        return CreateDataSource(configuration, logger, mockCatalog.Object);
    }

    private RootSettingsCollectionDataSource CreateDataSource(
        IConfigurationRoot configuration,
        ILogger<RootSettingsCollectionDataSource> logger,
        ISecurityProfileCatalog securityProfileCatalog
    )
    {
        var options = new OpenIdOptions
        {
            Server = { Settings = configuration.GetSection(SectionPath) },
        };

        var mockAlgorithmCollection = CreateStrictMock<IAlgorithmCollection>();
        mockAlgorithmCollection
            .Setup(x => x.GetEnumerator())
            .Returns(() => Enumerable.Empty<Algorithm>().GetEnumerator())
            .Verifiable();
        var mockAlgorithmProvider = CreateStrictMock<IAlgorithmCollectionProvider>();
        mockAlgorithmProvider
            .Setup(x => x.Collection)
            .Returns(mockAlgorithmCollection.Object)
            .Verifiable();
        mockAlgorithmProvider.Setup(x => x.GetChangeToken()).Returns(NeverChanges).Verifiable();

        var descriptors = new TestSettingDescriptorCollection(
            new SettingDescriptor<bool> { Name = "flag", AppliesTo = ["client"] }
        );
        var mockDescriptorProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        mockDescriptorProvider.Setup(x => x.Collection).Returns(descriptors).Verifiable();
        mockDescriptorProvider.Setup(x => x.GetChangeToken()).Returns(NeverChanges).Verifiable();

        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        mockFactory
            .Setup(x => x.Create())
            .Callback(() => CreatedCollectionCount++)
            .Returns(() => CreateLooseMock<ISettingCollection>().Object)
            .Verifiable();

        var mockScopeProvider = CreateStrictMock<ISettingScopeCollectionProvider>();
        mockScopeProvider
            .Setup(x => x.Collection)
            .Returns(TestSettingScopeCollection.OpenId)
            .Verifiable();

        return new RootSettingsCollectionDataSource(
            Options.Create(options),
            mockAlgorithmProvider.Object,
            mockDescriptorProvider.Object,
            mockFactory.Object,
            [],
            new DefaultSettingConfigurationReader(
                mockDescriptorProvider.Object,
                mockScopeProvider.Object
            ),
            securityProfileCatalog,
            logger
        );
    }

    private static IConfigurationRoot CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{SectionPath}:flag"] = "true" }
            )
            .Build();

    #region Collection Property Tests

    [Fact]
    public void Collection_IsTheServerSettingsExpandedWithTheProfilesTheySelect()
    {
        IReadOnlyCollection<Setting> expanded = [];
        IReadOnlyCollection<Setting>? capturedScope = null;
        IReadOnlyCollection<Setting>? capturedBaseline = null;
        var mockCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        mockCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<IReadOnlyCollection<Setting>>(),
                    It.IsAny<IReadOnlyCollection<Setting>>()
                )
            )
            .Callback(
                (IReadOnlyCollection<Setting> scope, IReadOnlyCollection<Setting> baseline) =>
                {
                    capturedScope = scope;
                    capturedBaseline = baseline;
                }
            )
            .Returns(expanded)
            .Verifiable();

        using var dataSource = CreateDataSource(
            CreateConfiguration(),
            CreateLooseMock<ILogger<RootSettingsCollectionDataSource>>().Object,
            mockCatalog.Object
        );

        Assert.Same(expanded, dataSource.Collection);
        Assert.NotNull(capturedScope);
        Assert.NotNull(capturedBaseline);
        Assert.NotSame(capturedBaseline, capturedScope);
    }

    #endregion

    #region Reload Tests

    [Fact]
    public void Reload_WhenSettingsCanBeHonored_ReloadsTheCollection()
    {
        var configuration = CreateConfiguration();
        using var dataSource = CreateDataSource(
            configuration,
            CreateLooseMock<ILogger<RootSettingsCollectionDataSource>>().Object
        );
        var initialCollection = dataSource.Collection;
        var changeToken = dataSource.GetChangeToken();

        configuration[$"{SectionPath}:flag"] = "false";
        configuration.Reload();

        Assert.NotSame(initialCollection, dataSource.Collection);
        Assert.Equal(4, CreatedCollectionCount);
        Assert.True(changeToken.HasChanged);
    }

    [Theory]
    [InlineData("tenant_issuer", "https://op.example")]
    [InlineData("flag", "yes")]
    public void Reload_WhenSettingsCannotBeHonored_KeepsThePreviousSettingsAndLogsAnError(
        string name,
        string value
    )
    {
        var configuration = CreateConfiguration();
        var mockLogger = CreateStrictMock<ILogger<RootSettingsCollectionDataSource>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
        using var dataSource = CreateDataSource(configuration, mockLogger.Object);
        var initialCollection = dataSource.Collection;
        var changeToken = dataSource.GetChangeToken();

        configuration[$"{SectionPath}:{name}"] = value;
        configuration.Reload();

        Assert.Same(initialCollection, dataSource.Collection);
        Assert.Equal(2, CreatedCollectionCount);
        Assert.False(changeToken.HasChanged);
    }

    #endregion
}
