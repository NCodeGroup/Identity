#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public sealed class DefaultDPoPNonceServiceTests : BaseTests
{
    private IDistributedCache DistributedCache { get; } =
        new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();

    private DefaultDPoPNonceService Service => new(DistributedCache);

    private OpenIdContext Context => CreateStrictMock<OpenIdContext>().Object;
    private OpenIdClient Client => CreateStrictMock<OpenIdClient>().Object;

    #region ValidateNonceAsync Tests

    [Fact]
    public async Task ValidateNonceAsync_WhenIssuedNonce_ReturnsTrue()
    {
        var service = Service;
        var context = Context;
        var client = Client;

        var nonce = await service.IssueNonceAsync(context, client, CancellationToken.None);
        var valid = await service.ValidateNonceAsync(
            context,
            client,
            nonce,
            CancellationToken.None
        );

        Assert.True(valid);
    }

    [Fact]
    public async Task ValidateNonceAsync_WhenUnknownNonce_ReturnsFalse()
    {
        var valid = await Service.ValidateNonceAsync(
            Context,
            Client,
            "unknown-nonce",
            CancellationToken.None
        );

        Assert.False(valid);
    }

    [Fact]
    public async Task ValidateNonceAsync_WhenEmpty_ReturnsFalse()
    {
        var valid = await Service.ValidateNonceAsync(
            Context,
            Client,
            string.Empty,
            CancellationToken.None
        );

        Assert.False(valid);
    }

    #endregion
}
