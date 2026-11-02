// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using NCode.Buffers;
using NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

namespace NCode.Identity.OpenId.LocalDev.Tests.DevelopmentSecrets;

public class ConformanceSeedSecretsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string ReadFile(string fileName) =>
        File.ReadAllText(Path.Combine(_root, fileName), SecureEncoding.UTF8);

    #region GetEnvironment Tests

    [Fact]
    public void GetEnvironment_Always_ConfiguresEachClientSecretAndTheTestUserPassword()
    {
        var environment = ConformanceSeedSecrets.GetEnvironment(_root);

        Assert.Equal(
            [
                "ConformanceSeed__Clients__client-secret-post__ClientSecret",
                "ConformanceSeed__Clients__client2__ClientSecret",
                "ConformanceSeed__Clients__client__ClientSecret",
                "ConformanceSeed__TestUserPassword",
            ],
            environment.Keys.Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void GetEnvironment_Always_ConfiguresTheClientSecretAsTheBase64UrlOfTheFileText()
    {
        var environment = ConformanceSeedSecrets.GetEnvironment(_root);

        var fileText = ReadFile("conformance-secret-client2");
        var expected = Base64Url.EncodeToString(SecureEncoding.UTF8.GetBytes(fileText));
        Assert.Equal(expected, environment["ConformanceSeed__Clients__client2__ClientSecret"]);
    }

    [Fact]
    public void GetEnvironment_Always_ConfiguresTheTestUserPasswordAsTheFileText()
    {
        var environment = ConformanceSeedSecrets.GetEnvironment(_root);

        var fileText = ReadFile("conformance-user-password");
        Assert.Equal(43, fileText.Length);
        Assert.Equal(fileText, environment["ConformanceSeed__TestUserPassword"]);
    }

    [Fact]
    public void GetEnvironment_Always_GeneratesADistinctSecretPerClient()
    {
        var environment = ConformanceSeedSecrets.GetEnvironment(_root);

        Assert.Equal(
            environment.Count,
            environment.Values.Distinct(StringComparer.Ordinal).Count()
        );
    }

    [Fact]
    public void GetEnvironment_WhenCalledTwice_ReturnsTheSameValues()
    {
        var first = ConformanceSeedSecrets.GetEnvironment(_root);

        var second = ConformanceSeedSecrets.GetEnvironment(_root);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetEnvironment_WhenAFileExists_KeepsItsValue()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "conformance-user-password"),
            "existing-password",
            SecureEncoding.UTF8
        );

        var environment = ConformanceSeedSecrets.GetEnvironment(_root);

        Assert.Equal("existing-password", environment["ConformanceSeed__TestUserPassword"]);
    }

    #endregion
}
