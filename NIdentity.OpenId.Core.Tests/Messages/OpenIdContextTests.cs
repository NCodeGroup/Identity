using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Serialization;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdContextTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdErrorFactory> MockOpenIdErrorFactory { get; }

    public OpenIdContextTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var context = new OpenIdContext(MockOpenIdErrorFactory.Object);

        Assert.Same(MockOpenIdErrorFactory.Object, context.ErrorFactory);

        Assert.True(context.JsonSerializerOptions.PropertyNameCaseInsensitive);
        Assert.Equal(JsonNamingPolicy.CamelCase, context.JsonSerializerOptions.PropertyNamingPolicy);
        Assert.Equal(JsonNumberHandling.AllowReadingFromString, context.JsonSerializerOptions.NumberHandling);

        Assert.Equal(JsonCommentHandling.Skip, context.JsonSerializerOptions.ReadCommentHandling);
        Assert.True(context.JsonSerializerOptions.AllowTrailingCommas);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is OpenIdMessageJsonConverterFactory);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IRequestClaim, RequestClaim>);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IRequestClaims, RequestClaims>);

        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is AuthorizationRequestJsonConverter);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>);
        Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>);
    }
}
