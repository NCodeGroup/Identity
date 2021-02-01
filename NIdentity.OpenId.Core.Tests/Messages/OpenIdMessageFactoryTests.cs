using System;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Authorization;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages
{
    public class OpenIdMessageFactoryTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public OpenIdMessageFactoryTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Constructor_ThenIsValid()
        {
            var factory = new OpenIdMessageFactory();

            var context = _mockOpenIdMessageContext.Object;
            var message = factory.Create<IAuthorizationRequestMessage>(context);
            Assert.IsType<AuthorizationRequestMessage>(message);
        }

        [Fact]
        public void Register_GivenNonExistingType_ThenAdded()
        {
            var factory = new OpenIdMessageFactory();

            factory.Register<ITestOpenIdMessage>(ctx => new TestOpenIdMessage { Context = ctx });

            var context = _mockOpenIdMessageContext.Object;
            var message = factory.Create<ITestOpenIdMessage>(context);
            Assert.IsType<TestOpenIdMessage>(message);
        }

        [Fact]
        public void Register_GivenExistingType_ThenNotAdded()
        {
            var factory = new OpenIdMessageFactory();

            factory.Register<ITestOpenIdMessage>(ctx => new TestOpenIdMessage { Context = ctx });
            factory.Register<ITestOpenIdMessage>(ctx => new TestOpenIdMessageWithKnownParameter { Context = ctx });

            var context = _mockOpenIdMessageContext.Object;
            var message = factory.Create<ITestOpenIdMessage>(context);
            Assert.IsType<TestOpenIdMessage>(message);
        }

        [Fact]
        public void Create_GivenNonExistingType_ThenThrows()
        {
            var factory = new OpenIdMessageFactory();

            var context = _mockOpenIdMessageContext.Object;

            Assert.Throws<InvalidOperationException>(() =>
            {
                factory.Create<ITestOpenIdMessage>(context);
            });
        }

        [Fact]
        public void Create_GivenExistingType_ThenContextIsSame()
        {
            var factory = new OpenIdMessageFactory();

            var context = _mockOpenIdMessageContext.Object;

            var message = factory.Create<IAuthorizationRequestMessage>(context);
            Assert.IsType<AuthorizationRequestMessage>(message);
            Assert.Same(context, message.Context);
        }
    }
}
