using System;
using GameLovers.Services;
using NSubstitute;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.Services.Tests
{
	public class MessageBrokerServiceTest
	{
		public interface IMockSubscriber
		{
			void MockMessageCall(MessageType1 message);
			void MockMessageCall2(MessageType1 message);
			void MockMessageAlternativeCall(MessageType2 message);
			void MockMessageAlternativeCall2(MessageType2 message);
		}
		
		public struct MessageType1 : IMessage {}
		public struct MessageType2 : IMessage {}

		private MessageType1 _messageType1;
		private MessageType2 _messageType2;
		private IMockSubscriber _subscriber;
		private MessageBrokerService _messageBroker;

		[SetUp]
		public void Init()
		{
			_messageBroker = new MessageBrokerService();
			_subscriber = Substitute.For<IMockSubscriber>();
			_messageType1 = new MessageType1();
			_messageType2 = new MessageType2();
		}

		[Test]
		// ADMIT: MessageBrokerService.Publish invokes every stored delegate for the message type.
		// RCR: MessageBrokerService.cs Publish — drop the `action(message)` invocation → RED (Received(2) sees only the
		// PublishSafe call). Broad: also reddens the other Received(n) tests. 2026-08-02
		public void Subscribe_Publish_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.Received(2).MockMessageCall(_messageType1);
		}

		[Test]
		// ADMIT: MessageBrokerService.Subscribe keys by action.Target and overwrites, so a second subscription from the
		// same object replaces the first.
		// RCR: MessageBrokerService.cs Subscribe — only add when the subscriber key is absent → RED (the first handler
		// still fires, DidNotReceive fails). 2026-08-02
		public void Subscribe_MultipleSubscriptionSameType_ReplacePreviousSubscription()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall2);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.Received(2).MockMessageCall2(_messageType1);
		}

		[Test]
		// ADMIT: MessageBrokerService.Publish raises the _isPublishing flag that makes a re-entrant Subscribe throw.
		// RCR: MessageBrokerService.cs Publish — set the publishing flag to false → RED (the chained Subscribe no longer
		// throws InvalidOperationException). 2026-08-02
		public void Publish_ChainSubscribe_ThrowsException()
		{
			_messageBroker.Subscribe<MessageType1>(m => _messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall));
			
			Assert.Throws<InvalidOperationException>(() => _messageBroker.Publish(_messageType1));
		}

		[Test]
		// ADMIT: MessageBrokerService.PublishSafe dispatches over the copied delegate array, which is what lets the
		// handler subscribe mid-publish.
		// RCR: MessageBrokerService.cs PublishSafe — drop the `action(message)` invocation → RED (the chained MessageType2
		// subscription never happens, Received(1) fails). 2026-08-02
		public void PublishSafe_ChainSubscribe_Succeeds()
		{
			_messageBroker.Subscribe<MessageType1>(m => _messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall));
			
			Assert.DoesNotThrow(() => _messageBroker.PublishSafe(_messageType1));
			_messageBroker.Publish(_messageType2);
			
			_subscriber.Received(1).MockMessageAlternativeCall(_messageType2);
		}

		[Test]
		// ADMIT: MessageBrokerService.Subscribe rejects a static method because action.Target is null and cannot key the
		// subscription map.
		// RCR: MessageBrokerService.cs Subscribe — return instead of throwing on a null Target → RED (no
		// ArgumentException). 2026-08-02
		public void Subscribe_StaticMethod_ThrowsException()
		{
			// The current implementation uses action.Target as the key. 
			// For static methods, action.Target is null, which is explicitly checked
			// and throws ArgumentException with a descriptive message.
			
			Assert.Throws<ArgumentException>(() => _messageBroker.Subscribe<MessageType1>(StaticMockCall));
		}

		private static void StaticMockCall(MessageType1 message) {}

		[Test]
		// ADMIT: MessageBrokerService.Publish early-returns when no subscription bucket exists for the message type.
		// RCR: MessageBrokerService.cs Publish — drop the early return from the missing-bucket guard → RED
		// (NullReferenceException iterating a null bucket, DoesNotThrow fails). 2026-08-02
		public void Publish_NoSubscribers_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => _messageBroker.Publish(_messageType1));
			Assert.DoesNotThrow(() => _messageBroker.PublishSafe(_messageType1));
		}

		[Test]
		// ADMIT: MessageBrokerService.Unsubscribe<T>(subscriber) removes that subscriber's delegate from the type's
		// bucket.
		// RCR: MessageBrokerService.cs Unsubscribe<T> — drop the per-subscriber removal → RED (the handler still fires
		// after unsubscribe). 2026-08-02
		public void Unsubscribe_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Unsubscribe<MessageType1>(_subscriber);
			_messageBroker.Publish(_messageType1);
			_messageBroker.PublishSafe(_messageType1);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
		}

		[Test]
		// ADMIT: Subscribe keys by action.Target so the same subscriber's second action replaces its first, and
		// Unsubscribe<T>(subscriber) then removes only that subscriber's entry from the message type's bucket.
		// RCR: MessageBrokerService.cs Unsubscribe<T> — `subscriptionObjects.Remove(subscriber);` →
		// `subscriptionObjects.Clear();` → RED (bystander.Received(2) sees 1). Isolated: Unsubscribe_Successfully has
		// a single subscriber, for which Clear and Remove are indistinguishable. 2026-08-04
		public void Subscribe_SameSubscriberSameType_LastActionWins_ThenUnsubscribeRemovesOnlyIt()
		{
			var bystander = Substitute.For<IMockSubscriber>();

			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall2);
			_messageBroker.Subscribe<MessageType1>(bystander.MockMessageCall);

			_messageBroker.Publish(_messageType1);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.Received(1).MockMessageCall2(_messageType1);

			_messageBroker.Unsubscribe<MessageType1>(_subscriber);
			_messageBroker.Publish(_messageType1);

			_subscriber.Received(1).MockMessageCall2(_messageType1);
			bystander.Received(2).MockMessageCall(_messageType1);
		}

		[Test]
		// ADMIT: MessageBrokerService.Unsubscribe<T>(null) drops only the requested message type's bucket, leaving other
		// types subscribed.
		// RCR: MessageBrokerService.cs Unsubscribe<T> — clear the whole subscription map instead of removing one type →
		// RED (MessageType2 handler stops receiving). 2026-08-02
		public void UnsubscribeWithoutAction_KeepsSubscriptionDifferentType_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall);
			_messageBroker.Unsubscribe<MessageType1>();
			_messageBroker.Publish(_messageType2);
			_messageBroker.PublishSafe(_messageType2);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.Received(2).MockMessageAlternativeCall(_messageType2);
		}

		[Test]
		// ADMIT: MessageBrokerService.UnsubscribeAll(null) clears every subscription bucket.
		// RCR: MessageBrokerService.cs UnsubscribeAll — drop the `_subscriptions.Clear()` call → RED (all four handlers
		// still receive). 2026-08-02
		public void UnsubscribeAll_Successfully()
		{
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall);
			_messageBroker.Subscribe<MessageType1>(_subscriber.MockMessageCall2);
			_messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall);
			_messageBroker.Subscribe<MessageType2>(_subscriber.MockMessageAlternativeCall2);
			_messageBroker.UnsubscribeAll();
			_messageBroker.Publish(_messageType1);
			_messageBroker.Publish(_messageType2);
			_messageBroker.PublishSafe(_messageType1);
			_messageBroker.PublishSafe(_messageType2);

			_subscriber.DidNotReceive().MockMessageCall(_messageType1);
			_subscriber.DidNotReceive().MockMessageCall2(_messageType1);
			_subscriber.DidNotReceive().MockMessageAlternativeCall(_messageType2);
			_subscriber.DidNotReceive().MockMessageAlternativeCall2(_messageType2);
		}

		[Test]
		// ADMIT: MessageBrokerService.Unsubscribe<T>(subscriber) early-returns when the message type has no bucket at all.
		// RCR: MessageBrokerService.cs Unsubscribe<T> — drop the early return from the missing-bucket guard → RED
		// (NullReferenceException, DoesNotThrow fails). 2026-08-02
		public void Unsubscribe_WithoutSubscription_DoesNothing()
		{
			Assert.DoesNotThrow(() => _messageBroker.Unsubscribe<MessageType1>(_subscriber));
			Assert.DoesNotThrow(() => _messageBroker.Unsubscribe<MessageType1>());
			Assert.DoesNotThrow(() => _messageBroker.UnsubscribeAll());
		}

		[Test]
		// ADMIT: MessageBrokerService.UnsubscribeAll(subscriber) removes that subscriber from every type bucket while
		// leaving other subscribers intact.
		// RCR: MessageBrokerService.cs UnsubscribeAll — drop the per-bucket removal → RED (subA still receives both
		// messages). 2026-08-02
		public void UnsubscribeAll_NonNullSubscriber_RemovesOnlyMatching()
		{
			var subA = Substitute.For<IMockSubscriber>();
			var subB = Substitute.For<IMockSubscriber>();

			_messageBroker.Subscribe<MessageType1>(subA.MockMessageCall);
			_messageBroker.Subscribe<MessageType2>(subA.MockMessageAlternativeCall);
			_messageBroker.Subscribe<MessageType1>(subB.MockMessageCall);
			_messageBroker.Subscribe<MessageType2>(subB.MockMessageAlternativeCall);

			_messageBroker.UnsubscribeAll(subA);

			_messageBroker.Publish(_messageType1);
			_messageBroker.Publish(_messageType2);

			subA.DidNotReceive().MockMessageCall(_messageType1);
			subA.DidNotReceive().MockMessageAlternativeCall(_messageType2);
			subB.Received(1).MockMessageCall(_messageType1);
			subB.Received(1).MockMessageAlternativeCall(_messageType2);
		}
	}
}
