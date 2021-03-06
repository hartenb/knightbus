using System.Linq;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Host.Tests.Unit.Processors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class StandardMessageProcessorProviderTests
    {
        [Test]
        public void Should_register_processor()
        {
            //arrange
            var provider = new StandardMessageProcessorProvider();
            //act
            provider.RegisterProcessor(new SingleCommandProcessor(Mock.Of<ICountable>()));
            //assert
            provider.ListAllProcessors().Count().Should().Be(1);
            provider.ListAllProcessors().FirstOrDefault().Should().Be(typeof(SingleCommandProcessor));
        }

        [Test]
        public void Should_get_registered_processor()
        {
            //arrange
            var provider = new StandardMessageProcessorProvider();
            var processor = new SingleCommandProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetProcessor<TestCommand>(typeof(IProcessCommand<TestCommand, TestTopicSettings>));
            //assert
            processorFound.Should().Be(processor);
        }

        [Test]
        public void Should_register_multi_processor()
        {
            //arrange
            var provider = new StandardMessageProcessorProvider();
            //act
            provider.RegisterProcessor(new MultipleCommandProcessor(Mock.Of<ICountable>()));
            //assert
            provider.ListAllProcessors().Count().Should().Be(1);
            provider.ListAllProcessors().Should().Contain(x => x == typeof(MultipleCommandProcessor));
        }
        [Test]
        public void Should_get_registered_multi_processor()
        {
            //arrange
            var provider = new StandardMessageProcessorProvider();
            var processor = new MultipleCommandProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetProcessor<TestCommandOne>(typeof(IProcessCommand<TestCommandOne, TestTopicSettings>));
            var processorFoundTwo = provider.GetProcessor<TestCommandTwo>(typeof(IProcessCommand<TestCommandTwo, TestTopicSettings>));
            //assert
            processorFound.Should().Be(processor);
            processorFoundTwo.Should().Be(processor);
        }
        [Test]
        public void Should_register_event_processor()
        {
            //arrange
            var provider = new StandardMessageProcessorProvider();
            //act
            provider.RegisterProcessor(new EventProcessor(Mock.Of<ICountable>()));
            //assert
            provider.ListAllProcessors().Count().Should().Be(1);
            provider.ListAllProcessors().Should().Contain(x => x == typeof(EventProcessor));
        }
        [Test]
        public void Should_get_registered_event_processor()
        {
            //arrange
            var provider = new StandardMessageProcessorProvider();
            var processor = new EventProcessor(Mock.Of<ICountable>());
            provider.RegisterProcessor(processor);
            //act
            var processorFound = provider.GetProcessor<TestEvent>(typeof(IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>));
            //assert
            processorFound.Should().Be(processor);
        }
    }
}