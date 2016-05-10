﻿using System;
using System.Text;
using Adaptive.Aeron.LogBuffer;
using Adaptive.Aeron.Samples.Common;
using Adaptive.Agrona.Concurrent;

namespace Adaptive.Aeron.Samples.SimpleSubscriber
{
    /// <summary>
    /// A very simple Aeron subscriber application which can receive small non-fragmented messages
    /// on a fixed channel and stream ID. The DataHandler method 'printStringMessage' is called when data
    /// is received. This application doesn't handle large fragmented messages. For an example of
    /// fragmented message reception, see <seealso cref="MultipleSubscribersWithFragmentAssembly"/>.
    /// </summary>
    public class SimpleSubscriber
    {
        public static void Main(string[] args)
        {
            // Maximum number of message fragments to receive during a single 'poll' operation
            const int fragmentLimitCount = 10;

            // The channel (an endpoint identifier) to receive messages from
            const string channel = "udp://localhost:40123";

            // A unique identifier for a stream within a channel. Stream ID 0 is reserved
            // for internal use and should not be used by applications.
            const int streamId = 10;

            Console.WriteLine("Subscribing to " + channel + " on stream Id " + streamId);

            var running = new AtomicBoolean(true);
            // Register a SIGINT handler for graceful shutdown.
            Console.CancelKeyPress += (s, e) => running.Set(false);

            // dataHandler method is called for every new datagram received
            IFragmentHandler fragmentHandler = new DelegateFragmentHandler((buffer, offset, length, header) =>
            {
                var data = new byte[length];
                buffer.GetBytes(offset, data);

                Console.WriteLine($"Received message ({Encoding.UTF8.GetString(data)}) to stream {streamId:D} from session {header.SessionId():x} term id {header.TermId():x} term offset {header.TermOffset():D} ({length:D}@{offset:D})");

                // Received the intended message, time to exit the program
                running.Set(false);
            });

            // Create a context, needed for client connection to media driver
            // A separate media driver process need to run prior to running this application
            var ctx = new Aeron.Context();

            // Create an Aeron instance with client-provided context configuration, connect to the
            // media driver, and add a subscription for the given channel and stream using the supplied
            // dataHandler method, which will be called with new messages as they are received.
            // The Aeron and Subscription classes implement AutoCloseable, and will automatically
            // clean up resources when this try block is finished.
            using (var aeron = Aeron.Connect(ctx))
            using (var subscription = aeron.AddSubscription(channel, streamId))
            {
                IIdleStrategy idleStrategy = new BusySpinIdleStrategy();

                // Try to read the data from subscriber
                while (running.Get())
                {
                    // poll delivers messages to the dataHandler as they arrive
                    // and returns number of fragments read, or 0
                    // if no data is available.
                    var fragmentsRead = subscription.Poll(fragmentHandler, fragmentLimitCount);
                    // Give the IdleStrategy a chance to spin/yield/sleep to reduce CPU
                    // use if no messages were received.
                    idleStrategy.Idle(fragmentsRead);
                }

                Console.WriteLine("Shutting down...");
            }
        }
    }
}