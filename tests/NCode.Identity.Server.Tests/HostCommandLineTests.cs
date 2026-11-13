// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Server.Tests;

public class HostCommandLineTests : BaseTests
{
    #region GetHostArguments Tests

    [Theory]
    [InlineData("setup", "setup")]
    [InlineData("setup", "setup", "extra")]
    [InlineData("secret-protection", "secret-protection", "rewrap", "--dry-run")]
    public void GetHostArguments_FirstArgumentIsTheCommand_ReturnsNoArguments(
        string commandName,
        params string[] args
    )
    {
        Assert.Empty(HostCommandLine.GetHostArguments(args, commandName));
    }

    [Theory]
    [InlineData("setup", "--urls", "http://localhost")]
    [InlineData("setup", "secret-protection", "rewrap")]
    [InlineData("setup", "--setup")]
    [InlineData("setup", "Setup")]
    [InlineData("setup", "--urls", "setup")]
    public void GetHostArguments_FirstArgumentIsNotTheCommand_ReturnsTheArguments(
        string commandName,
        params string[] args
    )
    {
        Assert.Same(args, HostCommandLine.GetHostArguments(args, commandName));
    }

    [Fact]
    public void GetHostArguments_NoArguments_ReturnsNoArguments()
    {
        Assert.Empty(HostCommandLine.GetHostArguments([], "setup"));
    }

    [Fact]
    public void GetHostArguments_NullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HostCommandLine.GetHostArguments(null!, "setup")
        );
    }

    [Fact]
    public void GetHostArguments_NullCommandName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HostCommandLine.GetHostArguments([], null!));
    }

    #endregion
}
