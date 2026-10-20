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
}
