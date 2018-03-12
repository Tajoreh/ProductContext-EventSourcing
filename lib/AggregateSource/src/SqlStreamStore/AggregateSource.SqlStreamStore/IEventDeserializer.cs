﻿using SqlStreamStore.Streams;
using System.Threading.Tasks;

namespace AggregateSource.SqlStreamStore
{
    /// <summary>
    /// Represent an event deserializer.
    /// </summary>
    public interface IEventDeserializer
    {
        /// <summary>
        /// Deserializes a resolved event into zero, one or more events consumable by the aggregate root entity.
        /// </summary>
        /// <param name="rawMessage">The resolved event to deserialize.</param>
        /// <returns>An enumeration of deserialized events.</returns>
        Task<object> DeserializeAsync(StreamMessage rawMessage);
    }
}
