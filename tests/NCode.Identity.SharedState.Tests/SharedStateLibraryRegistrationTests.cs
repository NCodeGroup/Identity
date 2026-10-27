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

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.SharedState.InProcess;
using Xunit;

namespace NCode.Identity.SharedState.Tests;

public sealed class SharedStateLibraryRegistrationTests : BaseTests
{
    #region AddInProcessExpiringKeySet Tests

    [Fact]
    public void AddInProcessExpiringKeySet_RegistersTheInProcessAdapter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSharedStateServices().AddInProcessExpiringKeySet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.IsType<DefaultInProcessExpiringKeySet>(
            provider.GetRequiredService<IExpiringKeySet>()
        );
    }

    [Fact]
    public void AddInProcessExpiringKeySet_WhenAdapterRegisteredFirst_KeepsThatAdapter()
    {
        var customKeySet = CreateStrictMock<IExpiringKeySet>().Object;
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(customKeySet);

        serviceCollection.AddSharedStateServices().AddInProcessExpiringKeySet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.Same(customKeySet, provider.GetRequiredService<IExpiringKeySet>());
    }

    #endregion

    #region AddInProcessExpiringCounterSet Tests

    [Fact]
    public void AddInProcessExpiringCounterSet_RegistersTheInProcessAdapter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSharedStateServices().AddInProcessExpiringCounterSet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.IsType<DefaultInProcessExpiringCounterSet>(
            provider.GetRequiredService<IExpiringCounterSet>()
        );
    }

    [Fact]
    public void AddInProcessExpiringCounterSet_WhenAdapterRegisteredFirst_KeepsThatAdapter()
    {
        var customCounterSet = CreateStrictMock<IExpiringCounterSet>().Object;
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(customCounterSet);

        serviceCollection.AddSharedStateServices().AddInProcessExpiringCounterSet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.Same(customCounterSet, provider.GetRequiredService<IExpiringCounterSet>());
    }

    #endregion

    #region AddInProcessExpiringValueSet Tests

    [Fact]
    public void AddInProcessExpiringValueSet_RegistersTheInProcessAdapter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSharedStateServices().AddInProcessExpiringValueSet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.IsType<DefaultInProcessExpiringValueSet>(
            provider.GetRequiredService<IExpiringValueSet>()
        );
    }

    [Fact]
    public void AddInProcessExpiringValueSet_WhenAdapterRegisteredFirst_KeepsThatAdapter()
    {
        var customValueSet = CreateStrictMock<IExpiringValueSet>().Object;
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(customValueSet);

        serviceCollection.AddSharedStateServices().AddInProcessExpiringValueSet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.Same(customValueSet, provider.GetRequiredService<IExpiringValueSet>());
    }

    #endregion

    #region AddInProcessExpiringIndexSet Tests

    [Fact]
    public void AddInProcessExpiringIndexSet_RegistersTheInProcessAdapter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSharedStateServices().AddInProcessExpiringIndexSet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.IsType<DefaultInProcessExpiringIndexSet>(
            provider.GetRequiredService<IExpiringIndexSet>()
        );
    }

    [Fact]
    public void AddInProcessExpiringIndexSet_WhenAdapterRegisteredFirst_KeepsThatAdapter()
    {
        var customIndexSet = CreateStrictMock<IExpiringIndexSet>().Object;
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(customIndexSet);

        serviceCollection.AddSharedStateServices().AddInProcessExpiringIndexSet();

        using var provider = serviceCollection.BuildServiceProvider();
        Assert.Same(customIndexSet, provider.GetRequiredService<IExpiringIndexSet>());
    }

    #endregion

    #region AddInProcessReplicaBroadcaster Tests

    [Fact]
    public void AddInProcessReplicaBroadcaster_RegistersTheInProcessAdapter()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSharedStateServices().AddInProcessReplicaBroadcaster();

        var descriptor = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IReplicaBroadcaster)
        );
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(DefaultInProcessReplicaBroadcaster), descriptor.ImplementationType);
    }

    [Fact]
    public void AddInProcessReplicaBroadcaster_WhenAdapterRegisteredFirst_KeepsThatAdapter()
    {
        var customBroadcaster = CreateStrictMock<IReplicaBroadcaster>().Object;
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(customBroadcaster);

        serviceCollection.AddSharedStateServices().AddInProcessReplicaBroadcaster();

        var descriptor = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IReplicaBroadcaster)
        );
        Assert.Same(customBroadcaster, descriptor.ImplementationInstance);
    }

    #endregion
}
