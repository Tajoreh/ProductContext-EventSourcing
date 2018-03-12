﻿#if !NET40
using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace AggregateSource.EventStore.Snapshots
{
    /// <summary>
    /// Represents an asynchronous, virtual collection of <typeparamref name="TAggregateRoot"/>.
    /// </summary>
    /// <typeparam name="TAggregateRoot">The type of the aggregate root in this collection.</typeparam>
    public class AsyncSnapshotableRepository<TAggregateRoot> : IAsyncRepository<TAggregateRoot>
        where TAggregateRoot : IAggregateRootEntity, ISnapshotable
    {
        readonly Func<TAggregateRoot> _rootFactory;
        readonly ConcurrentUnitOfWork _unitOfWork;
        readonly IEventStoreConnection _connection;
        readonly EventReaderConfiguration _configuration;
        readonly IAsyncSnapshotReader _reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRepository{TAggregateRoot}"/> class.
        /// </summary>
        /// <param name="rootFactory">The aggregate root entity factory.</param>
        /// <param name="unitOfWork">The unit of work to interact with.</param>
        /// <param name="connection">The event store connection to use.</param>
        /// <param name="configuration">The event store configuration to use.</param>
        /// <param name="reader">The snapshot reader to use.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="rootFactory"/> or <paramref name="unitOfWork"/> or <paramref name="connection"/> or <paramref name="configuration"/> or <paramref name="reader"/> is null.</exception>
        public AsyncSnapshotableRepository(Func<TAggregateRoot> rootFactory, ConcurrentUnitOfWork unitOfWork,
                                           IEventStoreConnection connection, EventReaderConfiguration configuration,
                                           IAsyncSnapshotReader reader)
        {
            if (rootFactory == null) throw new ArgumentNullException("rootFactory");
            if (unitOfWork == null) throw new ArgumentNullException("unitOfWork");
            if (connection == null) throw new ArgumentNullException("connection");
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (reader == null) throw new ArgumentNullException("reader");
            _rootFactory = rootFactory;
            _unitOfWork = unitOfWork;
            _connection = connection;
            _configuration = configuration;
            _reader = reader;
        }

        /// <summary>
        /// Gets the aggregate root entity associated with the specified aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <returns>An instance of <typeparamref name="TAggregateRoot"/>.</returns>
        /// <exception cref="AggregateNotFoundException">Thrown when an aggregate is not found.</exception>
        public async Task<TAggregateRoot> GetAsync(string identifier)
        {
            var result = await GetOptionalAsync(identifier);
            if (!result.HasValue)
                throw new AggregateNotFoundException(identifier, typeof (TAggregateRoot));
            return result.Value;
        }

        /// <summary>
        /// Attempts to get the aggregate root entity associated with the aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <returns>The found <typeparamref name="TAggregateRoot"/>, or empty if not found.</returns>
        public async Task<Optional<TAggregateRoot>> GetOptionalAsync(string identifier)
        {
            Aggregate aggregate;
            if (_unitOfWork.TryGet(identifier, out aggregate))
            {
                return new Optional<TAggregateRoot>((TAggregateRoot) aggregate.Root);
            }
            var snapshot = await _reader.ReadOptionalAsync(identifier);
            var version = 1;
            if (snapshot.HasValue)
            {
                version = snapshot.Value.Version + 1;
            }
            var streamUserCredentials = _configuration.StreamUserCredentialsResolver.Resolve(identifier);
            var streamName = _configuration.StreamNameResolver.Resolve(identifier);
            var slice =
                await
                _connection.ReadStreamEventsForwardAsync(streamName, version, _configuration.SliceSize, false,
                                                         streamUserCredentials);
            if (slice.Status == SliceReadStatus.StreamDeleted || slice.Status == SliceReadStatus.StreamNotFound)
            {
                return Optional<TAggregateRoot>.Empty;
            }
            var root = _rootFactory();
            if (snapshot.HasValue)
            {
                root.RestoreSnapshot(snapshot.Value.State);
            }
            root.Initialize(slice.Events.SelectMany(resolved => _configuration.Deserializer.Deserialize(resolved)));
            while (!slice.IsEndOfStream)
            {
                slice =
                    await
                    _connection.ReadStreamEventsForwardAsync(streamName, slice.NextEventNumber, _configuration.SliceSize,
                                                             false, streamUserCredentials);
                root.Initialize(slice.Events.SelectMany(resolved => _configuration.Deserializer.Deserialize(resolved)));
            }
            aggregate = new Aggregate(identifier, slice.LastEventNumber, root);
            _unitOfWork.Attach(aggregate);
            return new Optional<TAggregateRoot>(root);
        }

        /// <summary>
        /// Adds the aggregate root entity to this collection using the specified aggregate identifier.
        /// </summary>
        /// <param name="identifier">The aggregate identifier.</param>
        /// <param name="root">The aggregate root entity.</param>
        public void Add(string identifier, TAggregateRoot root)
        {
            _unitOfWork.Attach(new Aggregate(identifier, ExpectedVersion.NoStream, root));
        }
    }
}
#endif