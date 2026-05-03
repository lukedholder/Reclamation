using System;
using System.Collections.Generic;

namespace Reclamation.Simulation
{
    /// <summary>
    /// Synchronous, type-keyed event bus.
    ///
    /// Events are fired during simulation ticks and consumed by other systems
    /// and by the view layer (audio, particles, UI). All handlers run on the
    /// main thread in subscribe order — keep them short.
    ///
    /// Event types must be structs (value types) to avoid heap allocation per publish.
    /// See SimulationEvents.cs for all event definitions.
    ///
    /// Usage:
    ///   EventBus.Subscribe&lt;BlockPlacedEvent&gt;(OnBlockPlaced);
    ///   EventBus.Publish(new BlockPlacedEvent(block, construct));
    ///   EventBus.Unsubscribe&lt;BlockPlacedEvent&gt;(OnBlockPlaced);
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }

        public static void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            // Iterate by index — safe if a handler calls Unsubscribe during dispatch.
            for (int i = 0; i < list.Count; i++)
                ((Action<T>)list[i])(evt);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        /// <summary>
        /// Clears every subscription. Call this on simulation reset or scene unload
        /// to prevent stale view-layer handlers from holding simulation references.
        /// </summary>
        public static void Clear() => _handlers.Clear();
    }
}
