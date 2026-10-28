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

using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Pkce;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Pkce;

public class DefaultPkceSettingDescriptorDataSourceTests : BaseTests
{
    private const string CustomMethod = "S512";

    private DefaultPkceSettingDescriptorDataSource CreateDataSource(params string[] methodNames)
    {
        var mockProvider = CreateStrictMock<ICodeChallengeMethodCollectionProvider>();
        mockProvider.SetupGet(x => x.SupportedMethods).Returns(methodNames).Verifiable();

        return new DefaultPkceSettingDescriptorDataSource(
            CreateStrictMock<INullChangeToken>().Object,
            mockProvider.Object
        );
    }

    private static SettingDescriptor<IReadOnlyCollection<string>> GetDescriptor(
        DefaultPkceSettingDescriptorDataSource dataSource
    ) =>
        Assert.IsType<SettingDescriptor<IReadOnlyCollection<string>>>(
            Assert.Single(
                dataSource.Collection,
                descriptor => descriptor.Name == OpenIdSettingNames.CodeChallengeMethodsSupported
            )
        );

    #region Collection Property Tests

    [Fact]
    public void Collection_CodeChallengeMethodsSupported_DefaultsToS256Alone()
    {
        var dataSource = CreateDataSource(CustomMethod, OpenIdProtocol.CodeChallengeMethods.S256);

        var descriptor = GetDescriptor(dataSource);

        Assert.Equal([OpenIdProtocol.CodeChallengeMethods.S256], descriptor.Default);
    }

    [Fact]
    public void Collection_CodeChallengeMethodsSupported_WhenS256IsNotRegistered_DefaultsToEmpty()
    {
        var dataSource = CreateDataSource(CustomMethod);

        var descriptor = GetDescriptor(dataSource);

        Assert.Empty(descriptor.Default);
    }

    [Theory]
    [InlineData("S256", true)]
    [InlineData("S512", true)]
    [InlineData("plain", false)]
    [InlineData("s256", false)]
    public void Collection_CodeChallengeMethodsSupported_AcceptsOnlyRegisteredMethods(
        string value,
        bool expected
    )
    {
        var dataSource = CreateDataSource(OpenIdProtocol.CodeChallengeMethods.S256, CustomMethod);
        var descriptor = GetDescriptor(dataSource);
        IReadOnlyCollection<string> values = [value];

        var valid = descriptor.TryValidate(values, out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_CodeChallengeMethodsSupported_IsADiscoverableCeiling()
    {
        var dataSource = CreateDataSource(OpenIdProtocol.CodeChallengeMethods.S256);
        var descriptor = GetDescriptor(dataSource);
        IReadOnlyCollection<string> parent = [OpenIdProtocol.CodeChallengeMethods.S256];
        IReadOnlyCollection<string> child = [OpenIdProtocol.CodeChallengeMethods.S256, "plain"];

        var merged = descriptor.OnMerge(parent, child);

        Assert.True(descriptor.IsDiscoverable);
        Assert.Equal([OpenIdProtocol.CodeChallengeMethods.S256], merged);
    }

    #endregion
}
