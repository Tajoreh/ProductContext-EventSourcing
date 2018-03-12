﻿#if !NET40

using System;
using System.Linq;
using System.Threading.Tasks;

using EventStore.ClientAPI;

namespace AggregateSource.EventStore
{
    /// <summary>
    ///     Represents an asynchronous, virtual collection of <typeparamref name="TAggregateRoot" />.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root in this collection.</typeparam>
    public class AsyncRepository<TAggregateRoot> : IAsyncRepository<TAggregateRoot>
        where TAggregateRoot : IAggregateRootEntity
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="AsyncRepository{TAggregateRoot}" /> class.
        /// </summary>
        /// <param name="rootFactory">The aggregate root entity factory.</param>
        /// <param name="unitOfWork">The unit of work to interact with.</param>
        /// <param name="connection">The event store connection to use.</param>
        /// <param name="configuration">The event store configuration to use.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the <paramref name="rootFactory" /> or
        ///     <paramref name="unitOfWork" /> or <paramref name="connection" /> or <paramref name="configuration" /> is null.
        /// </exception>
        public AsyncRepository(Func<TAggregateRoot> rootFactory, ConcurrentUnitOfWork unitOfWork,
            IEventStoreConnection connection, EventReaderConfiguration configuration)
        {
            if (rootFactory == null)
            {
                throw new ArgumentNullException("rootFactory");
            }
            if (unitOfWork == null)
            {
                throw new ArgumentNullException("unitOfWork");
            }
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            RootFactory = rootFactory;
            UnitOfWork = unitOfWork;
            Connection = connection;
            Configuration = configuration;
        }

        /// <summary>
        ///     Gets the aggregate root entity factory.
        /// </summary>
        /// <value>
        ///     The aggregate root entity factory.
        /// </value>
        public Func<TAggregateRoot> RootFactory { get; }

        /// <summary>
        ///     Gets the unit of work.
        /// </summary>
        /// <value>
        ///     The unit of work.
        /// </value>
        public ConcurrentUnitOfWork UnitOfWork { get; }

        /// <summary>
        ///     Gets the event store connection to use.
        /// </summary>
        /// <value>
        ///     The event store connection to use.
        /// </value>
        public IEventStoreConnection Connection { get; }

        /// <summary>
        ///     Gets the event reader configuration.
        /// </summary>
        /// <value>
        ///     The event reader configuration.
        /// </value>
        public EventReaderConfiguration Configuration { get; }

        /// <summary>
        ///     Gets the aggregate root entity associated with the specified aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <returns>An instance of <typeparamref name="TAggregateRoot" />.</returns>
        /// <exception cref="AggregateNotFoundException">Thrown when an aggregate is not found.</exception>
        public async Task<TAggregateRoot> GetAsync(string identifier)
        {
            var result = await GetOptionalAsync(identifier);
            if (!result.HasValue)
            {
                throw new AggregateNotFoundException(identifier, typeof(TAggregateRoot));
            }
            return result.Value;
        }

        /// <summary>
        ///     Attempts to get the aggregate root entity associated with the aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <returns>The found <typeparamref name="TAggregateRoot" />, or empty if not found.</returns>
        public async Task<Optional<TAggregateRoot>> GetOptionalAsync(string identifier)
        {
            Aggregate aggregate;
            if (UnitOfWork.TryGet(identifier, out aggregate))
            {
                return new Optional<TAggregateRoot>((TAggregateRoot)aggregate.Root);
            }
            var streamUserCredentials = Configuration.StreamUserCredentialsResolver.Resolve(identifier);
            var streamName = Configuration.StreamNameResolver.Resolve(identifier);
            StreamEventsSlice slice =
                await
                    Connection.ReadStreamEventsForwardAsync(streamName, StreamPosition.Start, Configuration.SliceSize,
                        false, streamUserCredentials);
            if (slice.Status == SliceReadStatus.StreamDeleted || slice.Status == SliceReadStatus.StreamNotFound)
            {
                return Optional<TAggregateRoot>.Empty;
            }
            var root = RootFactory();
            root.Initialize(slice.Events.SelectMany(resolved => Configuration.Deserializer.Deserialize(resolved)));
            while (!slice.IsEndOfStream)
            {
                slice =
                    await
                        Connection.ReadStreamEventsForwardAsync(streamName, slice.NextEventNumber, Configuration.SliceSize,
                            false, streamUserCredentials);
                root.Initialize(slice.Events.SelectMany(resolved => Configuration.Deserializer.Deserialize(resolved)));
            }
            aggregate = new Aggregate(identifier, (int)slice.LastEventNumber, root);
            UnitOfWork.Attach(aggregate);
            return new Optional<TAggregateRoot>(root);
        }

        /// <summary>
        ///     Adds the aggregate root entity to this collection using the specified aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <param name="root">The aggregate root entity.</param>
        public void Add(string identifier, TAggregateRoot root)
        {
            UnitOfWork.Attach(new Aggregate(identifier, (int)ExpectedVersion.NoStream, root));
        }
    }
}

#endif
