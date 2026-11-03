// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Clients;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientAuthenticationMethodCatalogTests : BaseTests
{
    private static DefaultClientAuthenticationMethodCatalog CreateCatalog(
        params IEnumerable<ClientAuthenticationMethodDescriptor>[] dataSources
    ) =>
        new([
            .. dataSources.Select(
                items => new StaticDataSource<ClientAuthenticationMethodDescriptor>(items)
            ),
        ]);

    #region MethodNames Tests

    [Fact]
    public void MethodNames_KeepsTheRegisteredOrder()
    {
        var catalog = CreateCatalog(
            [
                new ClientAuthenticationMethodDescriptor
                {
                    Name = "zeta",
                    RequiresClientSecret = false,
                    UsesClientSecretAsKey = false,
                },
                new ClientAuthenticationMethodDescriptor
                {
                    Name = "alpha",
                    RequiresClientSecret = false,
                    UsesClientSecretAsKey = false,
                },
            ],
            [
                new ClientAuthenticationMethodDescriptor
                {
                    Name = "mu",
                    RequiresClientSecret = false,
                    UsesClientSecretAsKey = false,
                },
            ]
        );

        Assert.Equal(["zeta", "alpha", "mu"], catalog.MethodNames);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenTheMethodIsRegistered_ReturnsItsDescriptor()
    {
        var descriptor = new ClientAuthenticationMethodDescriptor
        {
            Name = "client_secret_basic",
            RequiresClientSecret = true,
            UsesClientSecretAsKey = false,
        };
        var catalog = CreateCatalog(
            [
                new ClientAuthenticationMethodDescriptor
                {
                    Name = "none",
                    RequiresClientSecret = false,
                    UsesClientSecretAsKey = false,
                },
            ],
            [descriptor]
        );

        var actual = catalog.TryGet("client_secret_basic", out var found);

        Assert.True(actual);
        Assert.Same(descriptor, found);
    }

    [Fact]
    public void TryGet_WhenTheMethodIsNotRegistered_ReturnsFalse()
    {
        var catalog = CreateCatalog([
            new ClientAuthenticationMethodDescriptor
            {
                Name = "none",
                RequiresClientSecret = false,
                UsesClientSecretAsKey = false,
            },
        ]);

        var actual = catalog.TryGet("client_secret_basic", out var found);

        Assert.False(actual);
        Assert.Null(found);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTwoDescriptorsShareAName_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(
                [
                    new ClientAuthenticationMethodDescriptor
                    {
                        Name = "none",
                        RequiresClientSecret = false,
                        UsesClientSecretAsKey = false,
                    },
                ],
                [
                    new ClientAuthenticationMethodDescriptor
                    {
                        Name = "none",
                        RequiresClientSecret = false,
                        UsesClientSecretAsKey = false,
                    },
                ]
            )
        );

        Assert.Equal(
            "More than one client-authentication method is named 'none'.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("client secret")]
    public void Constructor_WhenANameIsEmptyOrHasWhitespace_Throws(string name)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([
                new ClientAuthenticationMethodDescriptor
                {
                    Name = name,
                    RequiresClientSecret = false,
                    UsesClientSecretAsKey = false,
                },
            ])
        );

        Assert.Equal(
            "A client-authentication method descriptor must have a name that is not empty and contains no whitespace.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenADescriptorUsesTheSecretAsAKeyWithoutRequiringOne_Throws()
    {
        // A method that keys from the client_secret needs one issued. See ADR-1391.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([
                new ClientAuthenticationMethodDescriptor
                {
                    Name = "keyed_method",
                    RequiresClientSecret = false,
                    UsesClientSecretAsKey = true,
                },
            ])
        );

        Assert.Equal(
            "The client-authentication method 'keyed_method' uses the client secret as a key but does not require one.",
            exception.Message
        );
    }

    #endregion

    private sealed class StaticDataSource<T>(IEnumerable<T> items) : ICollectionDataSource<T>
    {
        public IEnumerable<T> Collection { get; } = [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}
