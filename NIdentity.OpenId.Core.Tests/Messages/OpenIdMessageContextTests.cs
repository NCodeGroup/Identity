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
    private MockRepository MockRepository { get; }
    private Mock<ILogger<OpenIdMessageContext>> MockLogger { get; }

    public OpenIdMessageContextTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockLogger = MockRepository.Create<ILogger<OpenIdMessageContext>>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var context = new OpenIdMessageContext(MockLogger.Object);

        Assert.Same(MockLogger.Object, context.Logger);

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
