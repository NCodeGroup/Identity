// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffBlazorClientLibraryTests : BaseTests
{
    #region BffBlazorClientLibrary

    [Fact]
    public void Marker_IsRegistrationMarker()
    {
        Assert.IsAssignableFrom<IRegistrationMarker<BffBlazorClientLibrary>>(
            default(BffBlazorClientLibrary)
        );
    }

    #endregion
}
