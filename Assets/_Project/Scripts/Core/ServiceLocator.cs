using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Core
{
    // Registry for game-wide services created by Bootstrap (state machine, world flags, configs...).
    // Use instead of FindObjectOfType / singletons: ServiceLocator.Get<GameStateMachine>().
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (services.ContainsKey(typeof(T)))
                Debug.LogWarning($"[ServiceLocator] {typeof(T).Name} is already registered, replacing it.");
            services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => services.Remove(typeof(T));

        public static T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException(
                $"[ServiceLocator] {typeof(T).Name} is not registered. Start Play from the Bootstrap scene.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out var s)) { service = (T)s; return true; }
            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => services.ContainsKey(typeof(T));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => services.Clear();
    }
}
