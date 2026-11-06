// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// The names of the browsers that the end-to-end tests run in, as theory data.
/// </summary>
public static class BrowserNames
{
    /// <summary>
    /// Gets Chromium and Firefox everywhere, and WebKit except on Windows.
    /// </summary>
    public static TheoryData<string> All { get; } = Create();

    private static TheoryData<string> Create()
    {
        var data = new TheoryData<string> { BrowserType.Chromium, BrowserType.Firefox };

        // Playwright's WebKit port on Windows does not use Safari's cookie stack and mishandles SameSite and Secure
        // (microsoft/playwright#20370), so WebKit runs where the port is faithful.
        if (!OperatingSystem.IsWindows())
            data.Add(BrowserType.Webkit);

        return data;
    }
}
