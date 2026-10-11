using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplayerShooting.Core.Architecture
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Subscribers = new();

        //subscribe
        public static void Subscribe<T>(Action<T> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            Type eventType = typeof(T);

            if (Subscribers.TryGetValue(eventType, out Delegate existing))
                Subscribers[eventType] = Delegate.Combine(existing, listener);
            else
                Subscribers.Add(eventType, listener);
        }

        //unsubscribe
        public static void UnSubscribe<T>(Action<T> listener)
        {
            if (listener == null)
                return;

            Type eventType = typeof(T);

            if (!Subscribers.TryGetValue(eventType, out Delegate existing))
                return;

            Delegate updated = Delegate.Remove(existing, listener);

            if (updated == null)
                Subscribers.Remove(eventType);
            else
                Subscribers[eventType] = updated;
        }

        //publist event to all subscribers
        public static void Publish<T>(T eventData)
        {
            if (!Subscribers.TryGetValue(typeof(T), out Delegate existing))
                return;

            Delegate snapshot = Delegate.Combine(existing);

            foreach (Action<T> listener in snapshot.GetInvocationList())
            {
                try
                {
                    listener.Invoke(eventData);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        // Remove all subscriptions.
        public static void Clear()
        {
            Subscribers.Clear();
        }
    }
}