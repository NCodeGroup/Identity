// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

/// <summary>
/// Shared scaffolding for the client-authentication handler tests: an <see cref="OpenIdContext"/> over a real
/// <see cref="DefaultHttpContext"/>, strict collaborators, and clients with a given registration.
/// </summary>
public abstract class ClientAuthenticationHandlerTestBase : BaseTests
{
    protected const string ClientId = "client-1";

    /// <summary>
    /// Gets the client-authentication method of the handler under test, which it audits on a failure.
    /// </summary>
    protected abstract string AuthenticationMethod { get; }

    protected Mock<IOpenIdClientResolver> MockClientResolver { get; }
    protected Mock<IOpenIdClientFactory> MockClientFactory { get; }
    protected Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    protected Mock<IClientAuthenticationAttemptLimiter> MockAttemptLimiter { get; }
    protected Mock<OpenIdContext> MockContext { get; }
    protected DefaultHttpContext HttpContext { get; } = new();

    protected ClientAuthenticationHandlerTestBase()
    {
        MockClientResolver = CreateStrictMock<IOpenIdClientResolver>();
        MockClientFactory = CreateStrictMock<IOpenIdClientFactory>();
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();

        // The strict limiter has no scaffold setup: a test on a path that asks the limiter or counts a failure sets up
        // that call, so a handler that asks or counts where it must not, or fails to where it must, fails the test.
        MockAttemptLimiter = CreateStrictMock<IClientAuthenticationAttemptLimiter>();

        MockContext = CreateStrictMock<OpenIdContext>();
        MockContext.SetupGet(x => x.Http).Returns(HttpContext).Verifiable();
    }

    protected ILogger<T> CreateLogger<T>() => CreateLooseMock<ILogger<T>>().Object;

    protected void SetupErrorFactory()
    {
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Returns<string>(code =>
            {
                // The response headers the handler sets are kept, so a test can assert them.
                var responseHeaders = new Dictionary<string, StringValues>(
                    StringComparer.OrdinalIgnoreCase
                );
                var mockError = CreateLooseMock<IOpenIdError>();
                mockError.SetupAllProperties();
                mockError
                    .Setup(x => x.SetResponseHeader(It.IsAny<string>(), It.IsAny<StringValues>()))
                    .Callback<string, StringValues>((name, value) => responseHeaders[name] = value);
                mockError.SetupGet(x => x.ResponseHeaders).Returns(responseHeaders);
                mockError.Object.Code = code;
                return mockError.Object;
            });
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    protected void SetupTenant()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.TenantId).Returns("tenant-1").Verifiable();
        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    protected void SetupForm(Dictionary<string, StringValues> fields)
    {
        HttpContext.Request.Method = HttpMethods.Post;
        HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
        HttpContext.Request.Form = new FormCollection(fields);
    }

    protected void SetupResolvedClient(OpenIdClient? client) =>
        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(MockContext.Object, ClientId, CancellationToken.None)
            )
            .ReturnsAsync(client)
            .Verifiable();

    protected void SetupAudit(string reason, string clientId = ClientId) =>
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordClientAuthenticationFailedAsync(
                    MockContext.Object,
                    clientId,
                    AuthenticationMethod,
                    reason,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    /// <summary>
    /// Admits the request at the limit of failed client authentications, for a path that asks it.
    /// </summary>
    protected void SetupAttemptAdmitted(string? clientId = ClientId) =>
        MockAttemptLimiter
            .Setup(x => x.CheckAsync(MockContext.Object, clientId, CancellationToken.None))
            .ReturnsAsync(RateLimitDecision.Admitted)
            .Verifiable();

    /// <summary>
    /// Refuses the request at the limit of failed client authentications, 90.5 seconds before its window ends.
    /// </summary>
    protected void SetupAttemptRefused(string? clientId = ClientId) =>
        MockAttemptLimiter
            .Setup(x => x.CheckAsync(MockContext.Object, clientId, CancellationToken.None))
            .ReturnsAsync(new RateLimitDecision(TimeSpan.FromSeconds(90.5)))
            .Verifiable();

    /// <summary>
    /// Expects the failure to be counted, with no cancellation token, for a path that refuses a credential.
    /// </summary>
    protected void SetupFailureCounted(string? clientId = ClientId) =>
        MockAttemptLimiter
            .Setup(x => x.RecordFailureAsync(MockContext.Object, clientId, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    /// <summary>
    /// Asserts a refusal at the limit of failed client authentications: <c>429</c>, <c>temporarily_unavailable</c>, a
    /// <c>Retry-After</c> rounded up to whole seconds and <c>Cache-Control: no-store</c>.
    /// </summary>
    protected static void AssertAttemptLimitRefusal(ClientAuthenticationResult result)
    {
        Assert.True(result.IsError);
        Assert.Equal(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable, result.Error.Code);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.Error.StatusCode);
        var headers = result.Error.ResponseHeaders;
        Assert.NotNull(headers);
        Assert.Equal("91", headers[HeaderNames.RetryAfter].ToString());
        Assert.Equal("no-store", headers[HeaderNames.CacheControl].ToString());
    }

    /// <summary>
    /// Creates a client registered for <paramref name="registeredMethod"/>. A <paramref name="ceiling"/> is set up only
    /// for a path that reads it, the client identifier only for a path that reports a failure, and whether the client
    /// is confidential only for a path that wraps the resolved client in a result, which the mutual-TLS handlers do
    /// only once the certificate matches.
    /// </summary>
    protected OpenIdClient CreateClient(
        string registeredMethod,
        IReadOnlyCollection<string>? ceiling,
        bool readsClientId,
        bool readsIsConfidential = true
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredMethod)
            .Verifiable();
        if (ceiling is not null)
        {
            var permittedMethods = ceiling;
            mockSettings
                .Setup(x =>
                    x.TryGetValue(
                        OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
                        out permittedMethods
                    )
                )
                .Returns(true)
                .Verifiable();
        }

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        if (readsIsConfidential)
            mockClient.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();
        if (readsClientId)
            mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        return mockClient.Object;
    }

    protected static void AssertInvalidClient(ClientAuthenticationResult result, int statusCode)
    {
        Assert.True(result.IsError);
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClient, result.Error.Code);
        Assert.Equal(statusCode, result.Error.StatusCode);
    }
}
