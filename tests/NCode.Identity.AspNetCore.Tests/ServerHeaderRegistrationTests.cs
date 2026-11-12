// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class ServerHeaderRegistrationTests : BaseTests
{
    #region AddServerHeaderSuppression Tests

    [Fact]
    public void AddServerHeaderSuppression_Valid_TurnsOffTheServerHeader()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();

        serviceCollection.AddServerHeaderSuppression();

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        Assert.False(options.AddServerHeader);
    }

    [Fact]
    public void AddServerHeaderSuppression_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ServerHeaderRegistration.AddServerHeaderSuppression(null!)
        );
    }

    #endregion
}
