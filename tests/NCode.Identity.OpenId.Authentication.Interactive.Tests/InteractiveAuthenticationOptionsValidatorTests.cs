// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Locales;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveAuthenticationOptionsValidatorTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IUiLocaleCatalog> MockCatalog { get; }

    public InteractiveAuthenticationOptionsValidatorTests()
    {
        MockCatalog = MockRepository.Create<IUiLocaleCatalog>();
    }

    public void Dispose() => MockRepository.Verify();

    private InteractiveAuthenticationOptionsValidator CreateValidator(params IUiLocale[] locales)
    {
        MockCatalog.SetupGet(x => x.Locales).Returns(locales).Verifiable();
        return new InteractiveAuthenticationOptionsValidator(MockCatalog.Object);
    }

    #region Validate Tests

    [Fact]
    public void Validate_WhenEveryRegisteredLocaleCarriesMessages_Succeeds()
    {
        var validator = CreateValidator(TestLocales.Xx, TestLocales.Yy);

        var result = validator.Validate(null, new InteractiveAuthenticationOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenARegisteredLocaleCarriesNoMessages_FailsNamingEachSuchTag()
    {
        var validator = CreateValidator(
            new PlainLocale("it"),
            TestLocales.Xx,
            new PlainLocale("pt-BR")
        );

        var result = validator.Validate(null, new InteractiveAuthenticationOptions());

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Collection(
            result.Failures,
            failure => Assert.Contains("'it'", failure, StringComparison.Ordinal),
            failure => Assert.Contains("'pt-BR'", failure, StringComparison.Ordinal)
        );
    }

    #endregion
}
