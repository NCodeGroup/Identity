// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class OwnerRootKeyUnavailableExceptionTranslatorTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockEnvironment { get; }

    public OwnerRootKeyUnavailableExceptionTranslatorTests()
    {
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    private static OwnerRootKeyUnavailableExceptionTranslator CreateTranslator(
        TimeSpan retryInterval
    )
    {
        var options = new SecretProtectionOptions
        {
            OwnerRootKeys = new OwnerRootKeyOptions { RetryInterval = retryInterval },
        };
        return new OwnerRootKeyUnavailableExceptionTranslator(Options.Create(options));
    }

    private void SetupCreateError()
    {
        MockEnvironment
            .Setup(x => x.CreateError(It.IsAny<string>()))
            .Returns<string>(code =>
            {
                // The response headers the translator sets are kept, so a test can assert them.
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
            })
            .Verifiable();
    }

    #region TryTranslate Tests

    [Fact]
    public void TryTranslate_OwnerRootKeyUnavailable_AnswersTemporarilyUnavailable()
    {
        SetupCreateError();
        var translator = CreateTranslator(TimeSpan.FromMinutes(1));
        var exception = new OwnerRootKeyUnavailableException(SecretOwner.ForTenant("acme"));

        var translated = translator.TryTranslate(MockEnvironment.Object, exception, out var error);

        Assert.True(translated);
        Assert.NotNull(error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable, error.Code);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
        Assert.Same(exception, error.Exception);
        var headers = error.ResponseHeaders;
        Assert.NotNull(headers);
        Assert.Equal("60", headers[HeaderNames.RetryAfter].ToString());
        Assert.Equal("no-store", headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public void TryTranslate_FractionalRetryInterval_RoundsRetryAfterUp()
    {
        SetupCreateError();
        var translator = CreateTranslator(TimeSpan.FromSeconds(90.5));
        var exception = new OwnerRootKeyUnavailableException(SecretOwner.ForTenant("acme"));

        var translated = translator.TryTranslate(MockEnvironment.Object, exception, out var error);

        Assert.True(translated);
        Assert.NotNull(error);
        Assert.NotNull(error.ResponseHeaders);
        Assert.Equal("91", error.ResponseHeaders[HeaderNames.RetryAfter].ToString());
    }

    [Fact]
    public void TryTranslate_AnotherException_IsNotAnswered()
    {
        var translator = CreateTranslator(TimeSpan.FromMinutes(1));

        var translated = translator.TryTranslate(
            MockEnvironment.Object,
            new InvalidOperationException("not owned"),
            out var error
        );

        Assert.False(translated);
        Assert.Null(error);
    }

    #endregion
}
