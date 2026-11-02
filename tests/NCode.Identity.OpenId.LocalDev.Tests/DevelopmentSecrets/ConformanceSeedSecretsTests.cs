// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Text.Json;
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

    // The golden fixture that build/tests/conformance-seed-credentials.tests.ps1 also reads, and the reference host's
    // conformance overlay; the project copies both beside the test assembly.
    private static readonly string GoldenFixturePath = Path.Combine(
        AppContext.BaseDirectory,
        "DevelopmentSecrets",
        "conformance-seed-golden.json"
    );

    private static readonly string ConformanceSettingsPath = Path.Combine(
        AppContext.BaseDirectory,
        "DevelopmentSecrets",
        "appsettings.Conformance.json"
    );

    private string ReadFile(string fileName) =>
        File.ReadAllText(Path.Combine(_root, fileName), SecureEncoding.UTF8);

    private static JsonElement ReadJson(string path) =>
        JsonElement.Parse(File.ReadAllText(path, SecureEncoding.UTF8));

    private static SortedDictionary<string, string> ReadStringMap(JsonElement element)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            map[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return map;
    }

    #region ClientIds Tests

    [Fact]
    public void ClientIds_Always_AreTheOverlayClientsThatAuthenticateWithAClientSecret()
    {
        var clients = ReadJson(ConformanceSettingsPath)
            .GetProperty("ConformanceSeed")
            .GetProperty("Clients");

        // A client that names no method uses client_secret_basic, the seed's default.
        var expected = new List<string>();
        foreach (var client in clients.EnumerateObject())
        {
            var method = client.Value.TryGetProperty("TokenEndpointAuthMethod", out var value)
                ? value.GetString()
                : null;
            if (
                string.IsNullOrEmpty(method)
                || method.StartsWith("client_secret_", StringComparison.Ordinal)
            )
            {
                expected.Add(client.Name);
            }
        }

        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            ConformanceSeedSecrets.ClientIds.Order(StringComparer.Ordinal)
        );
    }

    #endregion

    #region GetEnvironment Tests

    [Fact]
    public void GetEnvironment_ForTheGoldenFixtureFiles_ReturnsTheGoldenFixtureEnvironment()
    {
        var golden = ReadJson(GoldenFixturePath);
        Directory.CreateDirectory(_root);
        var files = ReadStringMap(golden.GetProperty("files"));
        foreach (var (fileName, text) in files)
        {
            File.WriteAllText(Path.Combine(_root, fileName), text, SecureEncoding.UTF8);
        }
        var expected = ReadStringMap(golden.GetProperty("environment"));

        var environment = ConformanceSeedSecrets.GetEnvironment(_root);

        Assert.Equal(
            expected,
            new SortedDictionary<string, string>(environment.ToDictionary(), StringComparer.Ordinal)
        );
        Assert.Equal(files.Count, Directory.GetFiles(_root).Length);
    }

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
        Assert.Equal(fileText, environment["ConformanceSeed__TestUserPassword"]);
    }

    [Fact]
    public void GetEnvironment_WhenTheFilesAreMissing_GeneratesA43CharacterPassword()
    {
        ConformanceSeedSecrets.GetEnvironment(_root);

        var fileText = ReadFile("conformance-user-password");
        Assert.Equal(43, fileText.Length);
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
