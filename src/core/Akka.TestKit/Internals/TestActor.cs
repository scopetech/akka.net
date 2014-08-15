using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Event;

namespace Akka.TestKit.Internals
{
    /// <summary>
    /// An actor that enqueues received messages to a <see cref="BlockingCollection{T}"/>.
    /// <remarks>Note! Part of internal API. Breaking changes may occur without notice. Use at own risk.</remarks>
    /// </summary>
    public class TestActor : ActorBase
    {
        private readonly BlockingCollection<MessageEnvelope> _queue;
        private TestKit.TestActor.Ignore _ignore;
        private AutoPilot _autoPilot;

        public TestActor(BlockingCollection<MessageEnvelope> queue)
        {
            _queue = queue;
        }

        protected override bool Receive(object message)
        {
            global::System.Diagnostics.Debug.WriteLine("TestActor received " + message);
            var setIgnore = message as TestKit.TestActor.SetIgnore;
            if(setIgnore != null)
            {
                _ignore = setIgnore.Ignore;
                return true;
            }
            var watch = message as TestKit.TestActor.Watch;
            if(watch != null)
            {
                Context.Watch(watch.Actor);
                return true;
            }
            var unwatch = message as TestKit.TestActor.Unwatch;
            if(unwatch != null)
            {
                Context.Unwatch(unwatch.Actor);
                return true;
            }
            var setAutoPilot = message as TestKit.TestActor.SetAutoPilot;
            if(setAutoPilot != null)
            {
                _autoPilot = setAutoPilot.AutoPilot;
                return true;
            }

            var actorRef = Sender;
            if(_autoPilot != null)
            {
                var newAutoPilot = _autoPilot.Run(actorRef, message);
                if(!(newAutoPilot is KeepRunning))
                    _autoPilot = newAutoPilot;
            }
            if(_ignore == null || !_ignore(message))
                _queue.Add(new RealMessageEnvelope(message, actorRef));
            return true;
        }

        protected override void PostStop()
        {
            var self = Self;
            MessageEnvelope message;
            while(_queue.TryTake(out message))
            {
                var messageSender = message.Sender;
                Context.System.DeadLetters.Tell(new DeadLetter(message.Message, messageSender, self), messageSender);
            }
        }


    }
}