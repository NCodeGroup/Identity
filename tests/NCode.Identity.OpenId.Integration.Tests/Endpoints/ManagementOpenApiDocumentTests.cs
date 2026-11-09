// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Reads the OpenAPI document the host serves and asserts the problem responses its management operations describe.
/// </summary>
public class ManagementOpenApiDocumentTests
{
    private static string[] HttpMethods { get; } = ["get", "put", "post", "patch", "delete"];

    private const string UnauthorizedDescription =
        "The request carries no valid management access token.";

    private const string BindingFailureDescription =
        "The request body is not valid JSON for this operation, or a parameter is missing or cannot be parsed.";

    private const string StoredSettingsConflictDescription =
        "A stored settings document the operation reads cannot be honored; replace it with a settings write.";

    private const string RootKeyUnavailableDescription =
        "A root key the operation needs is unavailable; retry after the interval in the Retry-After header.";

    #region Helpers

    private static async Task<JsonElement> GetDocumentAsync(HostApplicationFactory factory)
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonElement.Parse(body);
    }

    private static string GetResponseDescription(
        JsonElement document,
        string path,
        string method,
        string code
    ) =>
        document
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method)
            .GetProperty("responses")
            .GetProperty(code)
            .GetProperty("description")
            .GetString()
        ?? string.Empty;

    #endregion

    #region Operation Tests

    [Fact]
    public async Task ManagementOperations_Always_DescribeEveryProblemResponse()
    {
        using var factory = new HostApplicationFactory();

        var document = await GetDocumentAsync(factory);

        var seenPaths = 0;
        foreach (var pathEntry in document.GetProperty("paths").EnumerateObject())
        {
            if (!pathEntry.Name.StartsWith("/api/", StringComparison.Ordinal))
            {
                continue;
            }

            seenPaths++;
            foreach (var operation in pathEntry.Value.EnumerateObject())
            {
                if (!HttpMethods.Contains(operation.Name))
                {
                    continue;
                }

                if (!operation.Value.TryGetProperty("responses", out var responses))
                {
                    continue;
                }

                foreach (var response in responses.EnumerateObject())
                {
                    if (
                        !int.TryParse(
                            response.Name,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var code
                        )
                        || code < StatusCodes.Status400BadRequest
                    )
                    {
                        continue;
                    }

                    var description =
                        response.Value.TryGetProperty("description", out var value)
                        && value.ValueKind == JsonValueKind.String
                            ? value.GetString()
                            : null;

                    var message =
                        $"{operation.Name} {pathEntry.Name} {code.ToString(CultureInfo.InvariantCulture)}";
                    Assert.False(string.IsNullOrWhiteSpace(description), message);
                    Assert.True(description != ReasonPhrases.GetReasonPhrase(code), message);
                }
            }
        }

        Assert.True(seenPaths > 0);
    }

    [Fact]
    public async Task ManagementOperations_Always_DescribeTheSharedAnswers()
    {
        const string path = "/api/tenants/{tenantId}/grants";
        using var factory = new HostApplicationFactory();

        var document = await GetDocumentAsync(factory);

        Assert.Equal(UnauthorizedDescription, GetResponseDescription(document, path, "get", "401"));
        Assert.Equal(
            BindingFailureDescription,
            GetResponseDescription(document, path, "get", "400")
        );
        Assert.Equal(
            StoredSettingsConflictDescription,
            GetResponseDescription(document, path, "get", "409")
        );
        Assert.Equal(
            RootKeyUnavailableDescription,
            GetResponseDescription(document, path, "get", "503")
        );
    }

    [Fact]
    public async Task ManagementOperations_Always_LeadWithTheRoutesOwnAnswer()
    {
        using var factory = new HostApplicationFactory();

        var document = await GetDocumentAsync(factory);

        Assert.Equal(
            "The grant does not exist.",
            GetResponseDescription(
                document,
                "/api/tenants/{tenantId}/grants/{grantId}",
                "get",
                "404"
            )
        );
        Assert.Equal(
            "A scope with the same value already exists. " + StoredSettingsConflictDescription,
            GetResponseDescription(
                document,
                "/api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes",
                "post",
                "409"
            )
        );
    }

    #endregion
}
