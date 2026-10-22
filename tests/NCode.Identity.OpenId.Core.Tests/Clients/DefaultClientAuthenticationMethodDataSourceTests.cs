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
using NCode.Identity.OpenId.Authentication.Clients;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientAuthenticationMethodDataSourceTests : BaseTests
{
    private DefaultClientAuthenticationMethodDataSource CreateDataSource() =>
        new(CreateStrictMock<INullChangeToken>().Object);

    #region Collection Tests

    [Fact]
    public void Collection_ListsTheBuiltInMethodsInTheirAdvertisedOrder()
    {
        var dataSource = CreateDataSource();

        var names = dataSource.Collection.Select(descriptor => descriptor.Name).ToList();

        Assert.Equal(
            [
                OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
                OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
                OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
                OpenIdConstants.ClientAuthenticationMethods.ClientSecretJwt,
                OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth,
                OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
                OpenIdConstants.ClientAuthenticationMethods.None,
            ],
            names
        );
    }

    [Theory]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic, true)]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost, true)]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.ClientSecretJwt, true)]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt, false)]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth, false)]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth, false)]
    [InlineData(OpenIdConstants.ClientAuthenticationMethods.None, false)]
    public void Collection_RequiresAClientSecretOnlyForTheSecretMethods(
        string method,
        bool expected
    )
    {
        var dataSource = CreateDataSource();

        var descriptor = Assert.Single(
            dataSource.Collection,
            candidate => candidate.Name == method
        );

        Assert.Equal(expected, descriptor.RequiresClientSecret);
    }

    #endregion
}
