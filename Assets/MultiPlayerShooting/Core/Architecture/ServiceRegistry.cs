using System;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplayerShooting.Core.Architecture
{
    public static class ServiceRegistry
    {
        private static readonly Dictionary<Type, object> services = new();

        //register service by its type
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Type serviceType = typeof(T);

            if (services.ContainsKey(serviceType))
            {
                throw new InvalidOperationException(
                    $"Service {serviceType.Name} is already registered."
                );
            }

            services.Add(serviceType, service);
        }

        //get registered service
        public static T Get<T>() where T : class
        {
            Type serviceType = typeof(T);

            if (services.TryGetValue(serviceType, out object service))
            {
                return (T)service;
            }

            throw new InvalidOperationException(
                $"Service {serviceType.Name} is not registered."
            );
        }

        //check weather it has or not
        public static bool Has<T>() where T : class
        {
            return services.ContainsKey(typeof(T));
        }

        //unregister from services
        public static bool Unregister<T>() where T : class
        {
            return services.Remove(typeof(T));
        }

        //clear services
        public static void Clear()
        {
            services.Clear();
        }
    }
}