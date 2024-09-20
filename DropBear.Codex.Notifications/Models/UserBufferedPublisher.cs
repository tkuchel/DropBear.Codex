using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MessagePipe;

namespace DropBear.Codex.Notifications.Models
{
    /// <summary>
    /// Represents a publisher that buffers messages for specific users while also publishing them asynchronously.
    /// </summary>
    /// <typeparam name="TMessage">The type of message being published and buffered.</typeparam>
    public sealed class UserBufferedPublisher<TMessage>
    {
        private readonly IAsyncPublisher<TMessage> _publisher;
        private readonly ConcurrentDictionary<string, TMessage> _userBuffers = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="UserBufferedPublisher{TMessage}"/> class.
        /// </summary>
        /// <param name="publisher">The async publisher to use for publishing messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if publisher is null.</exception>
        public UserBufferedPublisher(IAsyncPublisher<TMessage> publisher)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        /// <summary>
        /// Publishes a message asynchronously and buffers it for the specific user.
        /// </summary>
        /// <param name="userId">The ID of the user to associate with the message.</param>
        /// <param name="message">The message to publish and buffer.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if userId or message is null.</exception>
        public async Task PublishAsync(string userId, TMessage message)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                // Publish the message to the subscriber
                await _publisher.PublishAsync(message).ConfigureAwait(false);

                // Buffer the message for future subscribers
                _userBuffers[userId] = message;
            }
            catch (Exception ex)
            {
                // Log the exception
                // Consider rethrowing or wrapping in a custom exception if needed
                throw new InvalidOperationException("Failed to publish or buffer the message.", ex);
            }
        }

        /// <summary>
        /// Retrieves the latest buffered message for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user whose message to retrieve.</param>
        /// <returns>The buffered message if found; otherwise, default(TMessage).</returns>
        /// <exception cref="ArgumentNullException">Thrown if userId is null or empty.</exception>
        public TMessage? GetBufferedMessage(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            return _userBuffers.TryGetValue(userId, out var message) ? message : default;
        }

        /// <summary>
        /// Clears the buffered message for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user whose message to clear.</param>
        /// <returns>True if the message was successfully removed; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if userId is null or empty.</exception>
        public bool ClearBufferedMessage(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            return _userBuffers.TryRemove(userId, out _);
        }
    }
}
