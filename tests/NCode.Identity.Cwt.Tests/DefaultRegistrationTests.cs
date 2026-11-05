// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cwt.Issuance;

namespace NCode.Identity.Cwt.Tests;

public class DefaultRegistrationTests
{
    #region AddCwtLibrary Tests

    [Fact]
    public async Task AddCwtLibrary_Registered_ResolvesTheIssuer()
    {
        await using var host = new CwtTestHost();

        Assert.NotNull(host.Services.GetRequiredService<ICwtIssuer>());
        Assert.Single(host.Services.GetServices<ICwtIssuer>());
    }

    #endregion
}
