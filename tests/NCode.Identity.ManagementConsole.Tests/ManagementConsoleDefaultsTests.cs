// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsoleDefaultsTests : BaseTests
{
    #region NoncePlaceholder Tests

    [Fact]
    public void NoncePlaceholder_Always_MatchesTheFrontendPreset()
    {
        Assert.Equal("__NCODE_CSP_NONCE__", ManagementConsoleDefaults.NoncePlaceholder);
    }

    #endregion
}
