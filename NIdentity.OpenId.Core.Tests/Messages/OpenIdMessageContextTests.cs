using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Moq;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Messages;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdMessageContextTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<HttpContext> MockHttpContext { get; }

    public OpenIdMessageContextTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockHttpContext = MockRepository.Create<HttpContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var context = new OpenIdContext(MockHttpContext.Object);

        Assert.Same(MockHttpContext.Object, context.HttpContext);

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
