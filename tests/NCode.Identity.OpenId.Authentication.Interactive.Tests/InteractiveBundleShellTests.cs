#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveBundleShellTests
{
    #region TryCreate Tests

    [Fact]
    public void TryCreate_WhenTheDocumentHasAMountPoint_SplitsItAfterTheHeadStartTagAndInsideTheMountPoint()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<html><HEAD lang=\"en\"><title>t</title></HEAD><body><div id=\"root\"></div><script></script></body></html>"
        );

        Assert.NotNull(shell);
        Assert.Equal("<html><HEAD lang=\"en\">", shell.Prelude.ToString());
        Assert.Equal("<title>t</title></HEAD><body><div id=\"root\">", shell.Head.ToString());
        Assert.Equal("</div><script></script></body></html>", shell.Tail.ToString());
    }

    [Fact]
    public void TryCreate_WhenAHeaderPrecedesTheHead_SplitsAfterTheHeadStartTag()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<!-- <header> --><head></head><body><div id=\"root\"></div></body>"
        );

        Assert.NotNull(shell);
        Assert.Equal("<!-- <header> --><head>", shell.Prelude.ToString());
    }

    [Theory]
    [InlineData("<body><div id=\"root\"></div></body>")]
    [InlineData("<header></header><body><div id=\"root\"></div></body>")]
    [InlineData("<head")]
    public void TryCreate_WhenNoHeadStartTagPrecedesTheMountPoint_ReturnsNull(string html)
    {
        var shell = InteractiveBundleShell.TryCreate(html + "<div id=\"root\"></div>");

        Assert.Null(shell);
    }

    [Fact]
    public void TryCreate_WhenTheMountPointIsNotEmpty_ReturnsNull()
    {
        var shell = InteractiveBundleShell.TryCreate("<body><div id=\"root\">x</div></body>");

        Assert.Null(shell);
    }

    [Fact]
    public void TryCreate_WhenTheDocumentHasNoMountPoint_ReturnsNull()
    {
        var shell = InteractiveBundleShell.TryCreate("<body></body>");

        Assert.Null(shell);
    }

    #endregion
}
