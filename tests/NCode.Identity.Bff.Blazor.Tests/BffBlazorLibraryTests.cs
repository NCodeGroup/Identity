// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public class BffBlazorLibraryTests : BaseTests
{
    #region BffBlazorLibrary Tests

    [Fact]
    public void BffBlazorLibrary_IsRegistrationMarker()
    {
        Assert.IsAssignableFrom<IRegistrationMarker<BffBlazorLibrary>>(default(BffBlazorLibrary));
    }

    #endregion
}
