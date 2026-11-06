// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public sealed class BffBlazorLibraryRequirementTests : BaseTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithoutMarker_ReturnsFailure()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var failure = BffBlazorLibraryRequirement.Instance.Validate(serviceProvider);

        Assert.Equal("AsBffComponents() requires AddBffBlazorLibrary().", failure);
    }

    [Fact]
    public void Validate_WithMarker_ReturnsNull()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IRegistrationMarker<BffBlazorLibrary>>(
            default(BffBlazorLibrary)
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var failure = BffBlazorLibraryRequirement.Instance.Validate(serviceProvider);

        Assert.Null(failure);
    }

    #endregion
}
