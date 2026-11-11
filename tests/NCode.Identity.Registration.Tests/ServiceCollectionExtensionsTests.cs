// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NCode.Identity.Registration.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    #region TryAddEnumerableInstance Tests

    [Fact]
    public void TryAddEnumerableInstance_WhenTheSameInstanceIsAddedTwice_RegistersItOnce()
    {
        var serviceCollection = new ServiceCollection();
        var instance = new Sample("a");

        serviceCollection.TryAddEnumerableInstance(instance);
        serviceCollection.TryAddEnumerableInstance(instance);

        var descriptor = Assert.Single(serviceCollection);
        Assert.Same(instance, descriptor.ImplementationInstance);
    }

    // TryAddEnumerable keeps one registration per implementation type, which would drop the second instance.
    [Fact]
    public void TryAddEnumerableInstance_WhenAnotherInstanceOfTheTypeIsAdded_KeepsBoth()
    {
        var serviceCollection = new ServiceCollection();
        var first = new Sample("a");
        var second = new Sample("b");

        serviceCollection.TryAddEnumerableInstance(first);
        serviceCollection.TryAddEnumerableInstance(second);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        Assert.Equal([first, second], serviceProvider.GetServices<Sample>());
    }

    [Fact]
    public void TryAddEnumerableInstance_WhenInstanceIsNull_Throws()
    {
        var serviceCollection = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            serviceCollection.TryAddEnumerableInstance<Sample>(null!)
        );
    }

    #endregion

    private sealed record Sample(string Name);
}
