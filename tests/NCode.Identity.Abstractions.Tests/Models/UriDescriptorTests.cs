#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Models;

public class UriDescriptorTests
{
    #region ToString Tests

    [Fact]
    public void ToString_WhenSchemeHostPath_ReturnsCanonicalUri()
    {
        var descriptor = new UriDescriptor
        {
            Scheme = "https",
            Host = new HostString("example.com"),
            Path = new PathString("/authorize"),
        };

        Assert.Equal("https://example.com/authorize", descriptor.ToString());
    }

    [Fact]
    public void ToString_WhenHostHasPort_IncludesPort()
    {
        var descriptor = new UriDescriptor
        {
            Scheme = "https",
            Host = new HostString("example.com", 8443),
            Path = new PathString("/token"),
        };

        Assert.Equal("https://example.com:8443/token", descriptor.ToString());
    }

    [Fact]
    public void ToString_WhenQueryPresent_IncludesQuery()
    {
        var descriptor = new UriDescriptor
        {
            Scheme = "https",
            Host = new HostString("example.com"),
            Path = new PathString("/authorize"),
            Query = new QueryString("?client_id=abc"),
        };

        Assert.Equal("https://example.com/authorize?client_id=abc", descriptor.ToString());
    }

    [Fact]
    public void ToString_WhenEmptyPath_OmitsPath()
    {
        var descriptor = new UriDescriptor
        {
            Scheme = "http",
            Host = new HostString("localhost"),
            Path = PathString.Empty,
        };

        Assert.Equal("http://localhost", descriptor.ToString());
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_WhenSet_RoundTrip()
    {
        var host = new HostString("example.com");
        var path = new PathString("/path");
        var query = new QueryString("?a=b");

        var descriptor = new UriDescriptor
        {
            Scheme = "https",
            Host = host,
            Path = path,
            Query = query,
        };

        Assert.Equal("https", descriptor.Scheme);
        Assert.Equal(host, descriptor.Host);
        Assert.Equal(path, descriptor.Path);
        Assert.Equal(query, descriptor.Query);
    }

    #endregion
}
