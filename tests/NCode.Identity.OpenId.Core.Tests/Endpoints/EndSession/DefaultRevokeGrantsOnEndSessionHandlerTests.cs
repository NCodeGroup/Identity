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
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public sealed class DefaultRevokeGrantsOnEndSessionHandlerTests : IDisposable
{
    private const string SubjectId = "subject-1";
    private const string SessionId = "sid-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private DefaultRevokeGrantsOnEndSessionHandler Handler { get; }

    public DefaultRevokeGrantsOnEndSessionHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockGrantStore = MockRepository.Create<IGrantStore>();

        Handler = new DefaultRevokeGrantsOnEndSessionHandler(
            TimeProvider.System,
            MockStoreManagerFactory.Object
        );
    }

    public void Dispose() => MockRepository.Verify();

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

    private static OnEndSessionCommand CreateCommand(string? subjectId, string? sessionId) =>
        new(Mock.Of<OpenIdContext>(), new EndSessionSubject(subjectId, sessionId, null, []));

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenSessionResolved_RevokesOnlyThatSessionsGrants()
    {
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    null,
                    null,
                    SessionId,
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(2)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(SubjectId, SessionId), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenNoSession_FallsBackToSubjectWideRevocation()
    {
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    SubjectId,
                    null,
                    null,
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(3)
            .Verifiable();

        await Handler.HandleAsync(
            CreateCommand(SubjectId, sessionId: null),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNeitherSubjectNorSession_DoesNothing()
    {
        // No store scaffold is set up: a strict-mock failure would mean the handler touched the store.
        await Handler.HandleAsync(
            CreateCommand(subjectId: null, sessionId: null),
            CancellationToken.None
        );
    }

    #endregion
}
