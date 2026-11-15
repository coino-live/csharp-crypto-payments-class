using System;
using System.Collections.Generic;

namespace CoinoLive
{
    /// <summary>
    /// The API refused the request; <see cref="Exception.Message"/> says why. The parent of every exception the
    /// client throws (<see cref="SignatureVerificationException"/> is separate).
    /// </summary>
    public class CoinoLiveException : Exception
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoErrors =
            new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>Creates the exception.</summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="status">The HTTP status, or 0 when there was no answer.</param>
        /// <param name="errors">Field errors, when the API names fields.</param>
        /// <param name="innerException">The cause, if any.</param>
        public CoinoLiveException(
            string message,
            int status = 0,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Status = status;
            Errors = errors ?? NoErrors;
        }

        /// <summary>The HTTP status, or 0 when Coino Live could not be reached.</summary>
        public int Status { get; }

        /// <summary>Field errors, when the API names fields: <c>{ "amount": ["…"] }</c>. Empty otherwise.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; }
    }

    /// <summary>Missing, wrong or revoked API key, or the gateway is suspended (401).</summary>
    public sealed class AuthenticationException : CoinoLiveException
    {
        /// <inheritdoc cref="CoinoLiveException(string, int, IReadOnlyDictionary{string, IReadOnlyList{string}}?, Exception?)"/>
        public AuthenticationException(string message, int status = 401, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
            : base(message, status, errors)
        {
        }
    }

    /// <summary>Not found, or it belongs to another gateway (404).</summary>
    public sealed class NotFoundException : CoinoLiveException
    {
        /// <inheritdoc cref="CoinoLiveException(string, int, IReadOnlyDictionary{string, IReadOnlyList{string}}?, Exception?)"/>
        public NotFoundException(string message, int status = 404, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
            : base(message, status, errors)
        {
        }
    }

    /// <summary>The request was refused; <see cref="CoinoLiveException.Errors"/> names the fields (422).</summary>
    public sealed class ValidationException : CoinoLiveException
    {
        /// <inheritdoc cref="CoinoLiveException(string, int, IReadOnlyDictionary{string, IReadOnlyList{string}}?, Exception?)"/>
        public ValidationException(string message, int status = 422, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
            : base(message, status, errors)
        {
        }
    }

    /// <summary>Too many requests (429). Retried automatically, then thrown.</summary>
    public sealed class RateLimitException : CoinoLiveException
    {
        /// <inheritdoc cref="CoinoLiveException(string, int, IReadOnlyDictionary{string, IReadOnlyList{string}}?, Exception?)"/>
        public RateLimitException(string message, int status = 429, IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
            : base(message, status, errors)
        {
        }
    }

    /// <summary>
    /// Coino Live could not be reached (network, DNS, timeout). Retried automatically, then thrown.
    /// <see cref="CoinoLiveException.Status"/> is 0.
    /// </summary>
    public sealed class ConnectionException : CoinoLiveException
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The cause, if any.</param>
        public ConnectionException(string message, Exception? innerException = null)
            : base(message, 0, null, innerException)
        {
        }
    }

    /// <summary>A webhook whose signature is missing, wrong or too old: refuse it.</summary>
    public sealed class SignatureVerificationException : Exception
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="message">Why the webhook was refused.</param>
        /// <param name="innerException">The cause, if any.</param>
        public SignatureVerificationException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
