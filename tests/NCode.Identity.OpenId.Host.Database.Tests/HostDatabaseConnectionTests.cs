// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace NCode.Identity.OpenId.Host.Database.Tests;

public sealed class HostDatabaseConnectionTests
{
    private const string ConnectionString =
        "Host=string-host;Database=string-db;Username=string-user;Password=string-secret";
    private const string Host = "db.example.test";
    private const string Port = "6543";
    private const string Name = "identity";
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
        string? name = Name,
        string? username = Username,
        string? password = Password
    ) =>
        [
            ("OpenIdDatabase:Host", host),
            ("OpenIdDatabase:Port", port),
            ("OpenIdDatabase:Name", name),
            ("OpenIdDatabase:Username", username),
            ("OpenIdDatabase:Password", password),
        ];

    #region Resolve Tests

    [Fact]
    public void Resolve_NothingConfigured_ReturnsNull()
    {
        var configuration = CreateConfiguration();

        var result = HostDatabaseConnection.Resolve(configuration);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyValues_ReturnsNull()
    {
        var configuration = CreateConfiguration([
            ("ConnectionStrings:openiddb", ""),
            .. CreateParts("", "", "", "", ""),
        ]);

        var result = HostDatabaseConnection.Resolve(configuration);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ConnectionStringOnly_ReturnsIt()
    {
        var configuration = CreateConfiguration(("ConnectionStrings:openiddb", ConnectionString));

        var result = HostDatabaseConnection.Resolve(configuration);

        Assert.Equal(ConnectionString, result);
    }

    [Fact]
    public void Resolve_Parts_BuildsConnectionString()
    {
        var configuration = CreateConfiguration(CreateParts());

        var result = HostDatabaseConnection.Resolve(configuration);

        Assert.NotNull(result);
        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(Host, builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal(Name, builder.Database);
        Assert.Equal(Username, builder.Username);
        Assert.Equal(Password, builder.Password);
    }

    [Fact]
    public void Resolve_PasswordWithSpecialCharacters_RoundTrips()
    {
        const string password = "p;a'ss=\"w=rd";
        var configuration = CreateConfiguration(CreateParts(password: password));

        var result = HostDatabaseConnection.Resolve(configuration);

        Assert.NotNull(result);
        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(password, builder.Password);
        Assert.Equal(Host, builder.Host);
        Assert.Equal(Name, builder.Database);
        Assert.Equal(Username, builder.Username);
    }

    [Fact]
    public void Resolve_PortOmitted_UsesNpgsqlDefault()
    {
        var configuration = CreateConfiguration(CreateParts(port: null));

        var result = HostDatabaseConnection.Resolve(configuration);

        Assert.NotNull(result);
        Assert.Equal(5432, new NpgsqlConnectionStringBuilder(result).Port);
    }

    [Fact]
    public void Resolve_ConnectionStringAndParts_Throws()
    {
        var configuration = CreateConfiguration([
            ("ConnectionStrings:openiddb", ConnectionString),
            .. CreateParts(),
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HostDatabaseConnection.Resolve(configuration)
        );

        Assert.Contains("ConnectionStrings:openiddb", exception.Message);
        Assert.Contains("OpenIdDatabase", exception.Message);
        Assert.DoesNotContain(ConnectionString, exception.Message);
        Assert.DoesNotContain(Password, exception.Message);
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Name")]
    [InlineData("Username")]
    [InlineData("Password")]
    public void Resolve_MissingRequiredPart_ThrowsNamingIt(string part)
    {
        var parts = CreateParts().Where(pair => pair.Key != $"OpenIdDatabase:{part}").ToArray();
        var configuration = CreateConfiguration(parts);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HostDatabaseConnection.Resolve(configuration)
        );

        Assert.Contains($"OpenIdDatabase:{part}", exception.Message);
        Assert.DoesNotContain(Password, exception.Message);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("65536")]
    public void Resolve_InvalidPort_Throws(string port)
    {
        var configuration = CreateConfiguration(CreateParts(port: port));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HostDatabaseConnection.Resolve(configuration)
        );

        Assert.Contains("OpenIdDatabase:Port", exception.Message);
        Assert.DoesNotContain(port, exception.Message);
        Assert.DoesNotContain(Host, exception.Message);
        Assert.DoesNotContain(Name, exception.Message);
        Assert.DoesNotContain(Username, exception.Message);
        Assert.DoesNotContain(Password, exception.Message);
    }

    #endregion
}
