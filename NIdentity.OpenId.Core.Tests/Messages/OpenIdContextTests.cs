#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Tenants;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdContextTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdServer> MockOpenIdServer { get; }
    private Mock<OpenIdTenant> MockOpenIdTenant { get; }
    private Mock<HttpContext> MockHttpContext { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<IPropertyBag> MockPropertyBag { get; }

    public OpenIdContextTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdServer = MockRepository.Create<OpenIdServer>();
        MockOpenIdTenant = MockRepository.Create<OpenIdTenant>();
        MockHttpContext = MockRepository.Create<HttpContext>();
        MockMediator = MockRepository.Create<IMediator>();
        MockPropertyBag = MockRepository.Create<IPropertyBag>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_ThenValid()
    {
        var openIdContext = new DefaultOpenIdContext(
            MockOpenIdServer.Object,
            MockOpenIdTenant.Object,
            MockHttpContext.Object,
            MockMediator.Object,
            MockPropertyBag.Object
        );

        Assert.Same(MockOpenIdTenant.Object, openIdContext.OpenIdTenant);
        Assert.Same(MockOpenIdServer.Object, openIdContext.OpenIdServer);
        Assert.Same(MockHttpContext.Object, openIdContext.HttpContext);
        Assert.Same(MockMediator.Object, openIdContext.Mediator);
    }

    // TODO: move to OpenIdServerTests
    // [Fact]
    // public void JsonSerializerOptions_Valid()
    // {
    //     var context = new DefaultOpenIdContext(
    //         MockHttpContext.Object,
    //         MockMediator.Object,
    //         MockOpenIdTenant.Object,
    //         MockPropertyBag.Object
    //     );
    //
    //     Assert.Same(context.JsonSerializerOptions, context.JsonSerializerOptions);
    //
    //     Assert.True(context.JsonSerializerOptions.PropertyNameCaseInsensitive);
    //     Assert.Equal(JsonNamingPolicy.CamelCase, context.JsonSerializerOptions.PropertyNamingPolicy);
    //     Assert.Equal(JsonNumberHandling.AllowReadingFromString, context.JsonSerializerOptions.NumberHandling);
    //
    //     Assert.Equal(JsonCommentHandling.Skip, context.JsonSerializerOptions.ReadCommentHandling);
    //     Assert.True(context.JsonSerializerOptions.AllowTrailingCommas);
    //
    //     Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is OpenIdMessageJsonConverterFactory);
    //
    //     Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IRequestClaim, RequestClaim>);
    //     Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IRequestClaims, RequestClaims>);
    //
    //     Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is AuthorizationRequestJsonConverter);
    //     Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>);
    //     Assert.Contains(context.JsonSerializerOptions.Converters, converter => converter is DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>);
    // }
}
