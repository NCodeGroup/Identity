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

using Moq;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultRevokeGrantsOnSessionTerminatedHandlerTests : BaseTests
{
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private DefaultRevokeGrantsOnSessionTerminatedHandler Handler { get; }

    public DefaultRevokeGrantsOnSessionTerminatedHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockGrantStore = CreateStrictMock<IGrantStore>();
        Handler = new DefaultRevokeGrantsOnSessionTerminatedHandler(
            TimeProvider.System,
            MockStoreManagerFactory.Object
        );
    }

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(MockGrantStore.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private static OnSessionTerminatedCommand CreateCommand(
        SessionTerminationReason reason,
        string? subjectId,
        string? sessionId
    ) => new(Mock.Of<OpenIdContext>(), new SessionTermination(reason, subjectId, sessionId, []));

    [Fact]
    public async Task HandleAsync_WhenSessionScoped_RevokesBySession()
    {
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.Is<GrantFilter>(f => f.SessionId == "sid-1" && f.SubjectId == null),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(2)
            .Verifiable();

        await Handler.HandleAsync(
            CreateCommand(SessionTerminationReason.AdministrativeRevocation, "subject-1", "sid-1"),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectWide_RevokesBySubject()
    {
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.Is<GrantFilter>(f => f.SubjectId == "subject-1" && f.SessionId == null),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(5)
            .Verifiable();

        await Handler.HandleAsync(
            CreateCommand(SessionTerminationReason.GlobalSignOut, "subject-1", sessionId: null),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNeitherScope_DoesNothing()
    {
        // No store scaffold is set up: a strict-mock failure would mean the handler touched the store.
        await Handler.HandleAsync(
            CreateCommand(SessionTerminationReason.GlobalSignOut, subjectId: null, sessionId: null),
            CancellationToken.None
        );
    }
}
