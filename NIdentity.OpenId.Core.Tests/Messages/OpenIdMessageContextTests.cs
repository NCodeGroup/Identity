using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Authorization;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdMessageContextTests : IDisposable
{
    private readonly MockRepository _mockRepository;
    private readonly Mock<ILogger<OpenIdMessageContext>> _mockLogger;

    public OpenIdMessageContextTests()
    {
        _mockRepository = new MockRepository(MockBehavior.Strict);
        _mockLogger = _mockRepository.Create<ILogger<OpenIdMessageContext>>();
    }

    public void Dispose()
    {
        _mockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var context = new OpenIdMessageContext(_mockLogger.Object);

        Assert.Same(_mockLogger.Object, context.Logger);

        Assert.True(context.JsonSerializerOptions.PropertyNameCaseInsensitive);
        Assert.Equal(JsonNamingPolicy.CamelCase, context.JsonSerializerOptions.PropertyNamingPolicy);
        Assert.Equal(JsonNumberHandling.AllowReadingFromString, context.JsonSerializerOptions.NumberHandling);

        Assert.Equal(JsonCommentHandling.Skip, context.JsonSerializerOptions.ReadCommentHandling);
        Assert.True(context.JsonSerializerOptions.AllowTrailingCommas);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is RequestClaimJsonConverter);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is RequestClaimsJsonConverter);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is OpenIdMessageJsonConverterFactory);
    }
}