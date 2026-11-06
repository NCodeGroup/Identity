// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("https://app.example/", "https://app.example/");
    }

    protected override void NavigateToCore(string uri, NavigationOptions options) =>
        throw new NotSupportedException();
}
