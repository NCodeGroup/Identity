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

using IdGen;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class DefaultOpenIdServerFactoryTests : BaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_DoesNotReadTheOptions()
    {
        var mockOptionsAccessor = CreateStrictMock<IOptions<OpenIdOptions>>();

        var factory = new DefaultOpenIdServerFactory(
            mockOptionsAccessor.Object,
            CreateStrictMock<IServiceProvider>().Object,
            CreateStrictMock<ISecretSerializer>().Object,
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICollectionDataSourceFactory>().Object,
            CreateStrictMock<IReadOnlySettingCollectionProviderFactory>().Object,
            CreateStrictMock<ISecretKeyCollectionProviderFactory>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object,
            CreateStrictMock<IIdGenerator<long>>().Object
        );

        Assert.NotNull(factory);
    }

    #endregion
}
