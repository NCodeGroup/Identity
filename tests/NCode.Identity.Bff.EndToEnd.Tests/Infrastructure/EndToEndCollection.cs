// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Shares one <see cref="EndToEndEnvironment"/> among every end-to-end test class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class EndToEndCollection : ICollectionFixture<EndToEndEnvironment>
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public const string Name = "End-to-end";
}
