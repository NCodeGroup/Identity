// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
                OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
                OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
                OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
                OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth,
                OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
                OpenIdProtocol.ClientAuthenticationMethods.None,
            ],
            names
        );
    }

    [Theory]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic, true)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost, true)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt, true)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt, false)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth, false)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth, false)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.None, false)]
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
