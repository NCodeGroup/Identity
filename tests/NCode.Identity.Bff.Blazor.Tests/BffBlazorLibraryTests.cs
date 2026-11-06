// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public class BffBlazorLibraryTests : BaseTests
{
    #region BffBlazorLibrary

    [Fact]
    public void Marker_IsRegistrationMarker()
    {
        Assert.IsAssignableFrom<IRegistrationMarker<BffBlazorLibrary>>(default(BffBlazorLibrary));
    }

    #endregion
}
