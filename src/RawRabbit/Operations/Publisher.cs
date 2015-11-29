﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using RawRabbit.Common;
using RawRabbit.Configuration.Publish;
using RawRabbit.Context;
using RawRabbit.Context.Provider;
using RawRabbit.Logging;
using RawRabbit.Operations.Contracts;
using RawRabbit.Serialization;

namespace RawRabbit.Operations
{
	public class Publisher<TMessageContext> : OperatorBase, IPublisher where TMessageContext : IMessageContext
	{
		private readonly IMessageContextProvider<TMessageContext> _contextProvider;
		private readonly ThreadLocal<Timer> _timer;
		private readonly ILogger _logger = LogManager.GetLogger<Publisher<TMessageContext>>();

		public Publisher(IChannelFactory channelFactory, IMessageSerializer serializer, IMessageContextProvider<TMessageContext> contextProvider)
			: base(channelFactory, serializer)
		{
			_contextProvider = contextProvider;
			_timer = new ThreadLocal<Timer>();
		}

		public Task PublishAsync<T>(T message, Guid globalMessageId, PublishConfiguration config)
		{
			var context = _contextProvider.GetMessageContext(globalMessageId);
			var channel = ChannelFactory.GetChannel();
			DeclareQueue(config.Queue, channel);
			DeclareExchange(config.Exchange, channel);
			var properties = new BasicProperties
			{
				MessageId = Guid.NewGuid().ToString(),
				Headers = new Dictionary<string, object>
						{
							{ _contextProvider.ContextHeaderName, context },
							{PropertyHeaders.Sent, DateTime.UtcNow.ToString("u") }
						}
			};

			channel.BasicPublish(
				exchange: config.Exchange.ExchangeName,
				routingKey: config.RoutingKey,
				basicProperties: properties,
				body: Serializer.Serialize(message)
				);
			channel.Dispose();
			return Task.FromResult(true);
		}

		public override void Dispose()
		{
			_logger.LogDebug("Disposing Publisher");
			base.Dispose();
			_timer.Dispose();
		}
	}
}
