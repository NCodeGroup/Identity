// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace NCode.Identity.SharedState.Valkey.Tests;

public sealed class ValkeyConnectionPartsTests
{
    private const string Host = "valkey.example.test";
    private const string Port = "6380";
    private const string Username = "identity-user";
    private const string Password = "pa55-w0rd";

    private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                pairs.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))
            )
            .Build();

    private static (string Key, string? Value)[] CreateParts(
        string? host = Host,
        string? port = Port,
        string? username = Username,
        string? password = Password,
        string? ssl = "true"
    ) =>
        [
            ("ValkeyConnection:Host", host),
            ("ValkeyConnection:Port", port),
            ("ValkeyConnection:Username", username),
            ("ValkeyConnection:Password", password),
            ("ValkeyConnection:Ssl", ssl),
        ];

    #region Resolve Tests

    [Fact]
    public void Resolve_NothingConfigured_ReturnsNull()
    {
        var configuration = CreateConfiguration();

        var result = ValkeyConnectionParts.Resolve(configuration);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyValues_ReturnsNull()
    {
        var configuration = CreateConfiguration(CreateParts("", "", "", "", ""));

        var result = ValkeyConnectionParts.Resolve(configuration);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_HostOnly_UsesDefaultPort()
    {
        var configuration = CreateConfiguration(("ValkeyConnection:Host", Host));

        var result = ValkeyConnectionParts.Resolve(configuration);

        Assert.NotNull(result);
        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(result.EndPoints));
        Assert.Equal(Host, endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.Null(result.User);
        Assert.Null(result.Password);
        Assert.False(result.Ssl);
    }

    [Fact]
    public void Resolve_AllParts_BuildsOptions()
    {
        var configuration = CreateConfiguration(CreateParts());

        var result = ValkeyConnectionParts.Resolve(configuration);

        Assert.NotNull(result);
        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(result.EndPoints));
        Assert.Equal(Host, endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
        Assert.Equal(Username, result.User);
        Assert.Equal(Password, result.Password);
        Assert.True(result.Ssl);
    }

    [Fact]
    public void Resolve_PasswordWithSpecialCharacters_RoundTrips()
    {
        const string SpecialPassword = "p,a'ss=\"w=rd";
        var configuration = CreateConfiguration(CreateParts(password: SpecialPassword));

        var result = ValkeyConnectionParts.Resolve(configuration);

        Assert.NotNull(result);
        Assert.Equal(SpecialPassword, result.Password);
    }

    [Fact]
    public void Resolve_PartsWithoutHost_ThrowsNamingIt()
    {
        var configuration = CreateConfiguration(CreateParts(host: null));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ValkeyConnectionParts.Resolve(configuration)
        );

        Assert.Contains("ValkeyConnection:Host", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("65536")]
    public void Resolve_InvalidPort_Throws(string port)
    {
        var configuration = CreateConfiguration(CreateParts(port: port));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ValkeyConnectionParts.Resolve(configuration)
        );

        Assert.Contains("ValkeyConnection:Port", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Host, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Username, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_InvalidSsl_Throws()
    {
        var configuration = CreateConfiguration(CreateParts(ssl: "maybe"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ValkeyConnectionParts.Resolve(configuration)
        );

        Assert.Contains("ValkeyConnection:Ssl", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("maybe", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_MisspelledPart_ThrowsNamingTheKeyButNotTheValue()
    {
        var configuration = CreateConfiguration(
            ("ValkeyConnection:Host", Host),
            ("ValkeyConnection:Pasword", Password)
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ValkeyConnectionParts.Resolve(configuration)
        );

        Assert.Contains("Pasword", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Password,
            exception.InnerException?.Message ?? string.Empty,
            StringComparison.Ordinal
        );
    }

    #endregion
}
