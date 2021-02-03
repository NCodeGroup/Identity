using System;
using Moq;
using NIdentity.OpenId.Messages;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages
{
    public class OpenIdMessageFactoryTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public OpenIdMessageFactoryTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockServiceProvider = _mockRepository.Create<IServiceProvider>();
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void CreateContext_ThenValid()
        {
            var factory = new OpenIdMessageFactory(_mockServiceProvider.Object);

            IOpenIdMessageContext LocalFactory()
            {
                return _mockOpenIdMessageContext.Object;
            }

            _mockServiceProvider
                .Setup(_ => _.GetService(typeof(Func<IOpenIdMessageContext>)))
                .Returns((object)new Func<IOpenIdMessageContext>(LocalFactory))
                .Verifiable();

            var context = factory.CreateContext();
            Assert.Same(_mockOpenIdMessageContext.Object, context);
        }

        [Fact]
        public void CreateMessage_ThenValid()
        {
            var context = _mockOpenIdMessageContext.Object;
            var factory = new OpenIdMessageFactory(_mockServiceProvider.Object);

            var mockTestOpenIdMessage = _mockRepository.Create<ITestOpenIdMessage>();

            ITestOpenIdMessage LocalFactory(IOpenIdMessageContext ctx)
            {
                Assert.Same(context, ctx);
                return mockTestOpenIdMessage.Object;
            }

            _mockServiceProvider
                .Setup(_ => _.GetService(typeof(Func<IOpenIdMessageContext, ITestOpenIdMessage>)))
                .Returns((object)new Func<IOpenIdMessageContext, ITestOpenIdMessage>(LocalFactory))
                .Verifiable();

            var message = factory.Create<ITestOpenIdMessage>(context);
            Assert.Same(mockTestOpenIdMessage.Object, message);
        }
    }
}
