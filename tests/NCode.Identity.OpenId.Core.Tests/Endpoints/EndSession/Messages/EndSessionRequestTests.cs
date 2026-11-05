// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession.Messages;

public class EndSessionRequestTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public EndSessionRequestTests()
    {
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    // The common parameters describe how each value a request carries is parsed, ui_locales as a space-separated
    // list; a clone copies the parsed parameters and consults nothing.
    private void SetupKnownParameters()
    {
        var dataSource = new DefaultCommonParameterDataSource(NullChangeToken.Singleton);
        var knownParameters = dataSource.Collection.ToDictionary(knownParameter =>
            knownParameter.Name
        );
        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(It.IsAny<string>()))
            .Returns(
                (string parameterName) =>
                    knownParameters.TryGetValue(parameterName, out var knownParameter)
                        ? new ParameterDescriptor(knownParameter)
                        : new ParameterDescriptor(parameterName, ParameterLoader.Default)
            )
            .Verifiable();
    }

    #region UiLocales Tests

    [Fact]
    public void UiLocales_WhenLoadedFromTheRequest_ParsesTheSpaceSeparatedTagsInOrder()
    {
        SetupKnownParameters();

        var request = EndSessionRequest.Load(
            MockOpenIdEnvironment.Object,
            [
                new KeyValuePair<string, StringValues>("id_token_hint", "hint"),
                new KeyValuePair<string, StringValues>("ui_locales", "fr-CA de"),
            ]
        );

        Assert.NotNull(request.UiLocales);
        Assert.Equal(["fr-CA", "de"], request.UiLocales);
        Assert.Equal("hint", request.IdTokenHint);
    }

    [Fact]
    public void UiLocales_WhenTheRequestCarriesNone_IsNull()
    {
        SetupKnownParameters();

        var request = EndSessionRequest.Load(
            MockOpenIdEnvironment.Object,
            [new KeyValuePair<string, StringValues>("id_token_hint", "hint")]
        );

        Assert.Null(request.UiLocales);
    }

    [Fact]
    public void UiLocales_WhenSet_SurvivesAClone()
    {
        var request = EndSessionRequest.Create(MockOpenIdEnvironment.Object);
        request.UiLocales = ["fr", "en"];

        var clone = request.Clone();

        Assert.NotNull(clone.UiLocales);
        Assert.Equal(["fr", "en"], clone.UiLocales);
    }

    #endregion
}
