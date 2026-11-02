// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Environments;

public class DefaultOpenIdEnvironmentFactoryTests : BaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_DoesNotReadTheOptions()
    {
        var mockOptionsAccessor = CreateStrictMock<IOptions<OpenIdOptions>>();

        var factory = new DefaultOpenIdEnvironmentFactory(
            NullLoggerFactory.Instance,
            mockOptionsAccessor.Object,
            CreateStrictMock<IDataProtectionProvider>().Object,
            CreateStrictMock<IKnownParameterCollectionProvider>().Object,
            CreateStrictMock<IOpenIdMessageFactorySelector>().Object,
            []
        );

        Assert.NotNull(factory);
    }

    #endregion
}
