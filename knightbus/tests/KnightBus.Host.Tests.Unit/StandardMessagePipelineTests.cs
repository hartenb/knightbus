using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Host.Tests.Unit.Processors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class StandardMessagePipelineTests
    {
        private IMessageProcessor _messageProcessor;
        private Mock<IMessageStateHandler<TestCommandOne>> _stateHandler;
        private Mock<ICountable> _countable;
        private Mock<ILog> _logger;
        private Mock<IMessageProcessorProvider> _messageHandlerProvider;


        [SetUp]
        public void Setup()
        {
            _messageHandlerProvider = new Mock<IMessageProcessorProvider>();
            _logger = new Mock<ILog>();
            _stateHandler = new Mock<IMessageStateHandler<TestCommandOne>>();
            _countable = new Mock<ICountable>();
            _messageHandlerProvider.Setup(x => x.GetProcessor<TestCommandOne>(typeof(MultipleCommandProcessor))).Returns(
                () => new MultipleCommandProcessor(_countable.Object)
            );
            var middlewares = new List<IMessageProcessorMiddleware>
            {
                new ThrottlingMiddleware(1)
            };
            var transportConfiguration = new Mock<ITransportChannelFactory>();
            transportConfiguration.Setup(x => x.Middlewares).Returns(new List<IMessageProcessorMiddleware>());
            var pipeline = new MiddlewarePipeline(middlewares, transportConfiguration.Object, _logger.Object);
            _messageProcessor = pipeline.GetPipeline(new MessageProcessor<MultipleCommandProcessor>(_messageHandlerProvider.Object));
        }

        [Test]
        public async Task Should_process_message()
        {
            //arrange
            _stateHandler.Setup(x => x.DeliveryCount).Returns(1);
            _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            _stateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(new TestCommandOne());
            //act
            await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
            //assert
            _stateHandler.Verify(x => x.CompleteAsync(), Times.Once);
            _countable.Verify(x => x.Count(), Times.Once);

        }
        [Test]
        public async Task Should_deadletter_message()
        {
            //arrange
            _stateHandler.Setup(x => x.DeliveryCount).Returns(2);
            _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            //act
            await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
            //assert
            _stateHandler.Verify(x => x.DeadLetterAsync(1), Times.Once);
            _countable.Verify(x => x.Count(), Times.Never);
        }
        [Test]
        public async Task Should_abandon_message()
        {
            //arrange
            _stateHandler.Setup(x => x.DeliveryCount).Returns(1);
            _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            _stateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(new TestCommandOne { Throw = true });
            //act
            await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
            //assert
            _stateHandler.Verify(x => x.AbandonByErrorAsync(It.IsAny<Exception>()), Times.Once);
        }

        [Test]
        public async Task Should_log_error()
        {
            //arrange
            _stateHandler.Setup(x => x.DeliveryCount).Returns(1);
            _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            _stateHandler.Setup(x => x.GetMessageAsync()).ReturnsAsync(new TestCommandOne { Throw = true });
            //act
            await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
            //assert
            _logger.Verify(x => x.Error(It.IsAny<Exception>(), "Error processing message {@TestCommandOne}", It.IsAny<TestCommandOne>()), Times.Once);
        }

        
    }
}