// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffSessionScopeExtensionsTests : BaseTests
{
    #region RequireFrontend Tests

    [Fact]
    public void RequireFrontend_WithFrontend_ReturnsIt()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var scope = CreateStrictMock<IBffSessionScope>();
        scope.Setup(x => x.SelectedFrontend).Returns(frontend).Verifiable();

        var result = scope.Object.RequireFrontend();

        Assert.Same(frontend, result);
    }

    [Fact]
    public void RequireFrontend_WithoutFrontend_Throws()
    {
        var scope = CreateStrictMock<IBffSessionScope>();
        scope.Setup(x => x.SelectedFrontend).Returns((BffFrontend?)null).Verifiable();

        Assert.Throws<InvalidOperationException>(() => scope.Object.RequireFrontend());
    }

    #endregion
}
