using Grpc.Core;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides services for creating service clients (proxies)
    /// </summary>
    public abstract class ClientFactory
    {
        /// <summary>
        /// The default client factory (uses the default BinderConfiguration)
        /// </summary>
        public static ClientFactory Default { get; } = DefaultClientFactory.Instance;

        /// <summary>
        /// Create a new client factory; note that non-default factories should be considered expensive, and stored/re-used suitably
        /// </summary>
        public static ClientFactory Create(BinderConfiguration? binderConfiguration = null)
            => (binderConfiguration == null || binderConfiguration == BinderConfiguration.Default) ? Default : new ConfiguredClientFactory(binderConfiguration);

        /// <summary>
        /// Get the binder configuration associated with this instance
        /// </summary>
        protected abstract BinderConfiguration BinderConfiguration { get; }

        /// <summary>
        /// Get the binder configuration associated with this instance
        /// </summary>
        public static implicit operator BinderConfiguration(ClientFactory value) => value.BinderConfiguration;

        /// <summary>
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract TService CreateClient<TService>(CallInvoker channel) where TService : class;

        /// <summary>
        /// Gets the concrete client type that would provide this service
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract Type GetClientType<TService>() where TService : class;

        /// <summary>
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        public virtual GrpcClient CreateClient(CallInvoker channel, Type contractType)
            => new GrpcClient(channel, contractType, BinderConfiguration);

        private sealed class ConfiguredClientFactory : ClientFactory
        {
            protected override BinderConfiguration BinderConfiguration { get; }

            public ConfiguredClientFactory(BinderConfiguration? binderConfiguration)
            {
                BinderConfiguration = binderConfiguration ?? BinderConfiguration.Default;
            }

            private readonly struct StubPair
            {
                public readonly Delegate Factory;
                public readonly Type ConcreteType;
                public StubPair (Delegate factory, Type concreteType)
                {
                    Factory = factory;
                    ConcreteType = concreteType;
                }
            }
            private readonly ConcurrentDictionary<Type, StubPair> _stubCache = new ConcurrentDictionary<Type, StubPair>();

            [MethodImpl(MethodImplOptions.NoInlining)]
            private StubPair SlowCreateStub<TService>()
                where TService : class
            {
                var factory = ProxyEmitter.CreateFactory<TService>(BinderConfiguration, out var concreteType);
                var stub = new StubPair(factory, concreteType);

                var key = typeof(TService);
                return _stubCache.TryAdd(key, stub) ? stub : _stubCache[key];
            }
            public override TService CreateClient<TService>(CallInvoker channel)
                where TService : class
            {
                if (!_stubCache.TryGetValue(typeof(TService), out var stub))
                {
                    stub = SlowCreateStub<TService>();
                }
                return ((Func<CallInvoker, TService>)stub.Factory)(channel);
            }

            public override Type GetClientType<TService>()
            {
                if (!_stubCache.TryGetValue(typeof(TService), out var stub))
                {
                    stub = SlowCreateStub<TService>();
                }
                return stub.ConcreteType;
            }
        }

        internal static class DefaultProxyCache<TService> where TService : class
        {
#pragma warning disable CS8618 // Non-nullable field is uninitialized - actually initialized vs "out"
            internal static readonly Type ConcreteType;
#pragma warning restore CS8618
            internal static readonly Func<CallInvoker, TService> Create = ProxyEmitter.CreateFactory<TService>(BinderConfiguration.Default, out ConcreteType);
        }

        private sealed class DefaultClientFactory : ClientFactory
        {
            protected override BinderConfiguration BinderConfiguration => BinderConfiguration.Default;

            public static readonly DefaultClientFactory Instance = new DefaultClientFactory();
            private DefaultClientFactory() { }

            public override TService CreateClient<TService>(CallInvoker channel) => DefaultProxyCache<TService>.Create(channel);

            public override Type GetClientType<TService>() => DefaultProxyCache<TService>.ConcreteType;
        }
    }
}
