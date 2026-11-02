// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Core.Tests.Messages;

internal class TestOpenIdMessageNoDefaultConstructor : OpenIdMessage
{
    // ReSharper disable once UnusedParameter.Local
    public TestOpenIdMessageNoDefaultConstructor(object unused)
    {
        // nothing
    }
}
