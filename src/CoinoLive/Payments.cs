using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoinoLive
{
    /// <summary>Payments: create one per order, send the customer to its checkout_url, read it back before shipping.</summary>
    public sealed class Payments
    {
        private readonly CoinoLiveClient _client;

        internal Payments(CoinoLiveClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Creates a payment. Without an idempotency key a random one is used, so a retried request never creates a
        /// second payment. Pass your own key (for example "order-1001-1") to make repeating the call from your code safe too.
        /// </summary>
        /// <param name="parameters">The amount and the optional fields.</param>
        /// <param name="idempotencyKey">Up to 128 letters, digits and -_:. characters; random when null.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Payment> CreateAsync(CreatePaymentParams parameters, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            string key = string.IsNullOrEmpty(idempotencyKey) ? CoinoLiveClient.NewIdempotencyKey() : idempotencyKey;
            return _client.DataAsync<Payment>(HttpMethod.Post, "/payments", cancellationToken, parameters, key);
        }

        /// <summary>Reads a payment.</summary>
        /// <param name="paymentId">The payment ID.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Payment> RetrieveAsync(string paymentId, CancellationToken cancellationToken = default)
        {
            return _client.DataAsync<Payment>(HttpMethod.Get, "/payments/" + CoinoLiveClient.Id(paymentId, nameof(paymentId)), cancellationToken);
        }

        /// <summary>One page of payments (50), newest first: <c>{ data, meta: { next_cursor, prev_cursor } }</c>.</summary>
        /// <param name="status">Only payments with this status (see <see cref="PaymentStatus"/>).</param>
        /// <param name="orderId">Only payments for this order.</param>
        /// <param name="cursor">A cursor from <see cref="PageMeta"/>.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Page<Payment>> ListAsync(string? status = null, string? orderId = null, string? cursor = null, CancellationToken cancellationToken = default)
        {
            string query = CoinoLiveClient.Query(("status", status), ("order_id", orderId), ("cursor", cursor));
            return _client.RequestAsync<Page<Payment>>(HttpMethod.Get, "/payments" + query, null, null, false, cancellationToken);
        }

        /// <summary>
        /// Every payment matching the filters, fetching pages as you go:
        /// <c>await foreach (var payment in coino.Payments.AllAsync(status: PaymentStatus.Paid))</c>.
        /// </summary>
        /// <param name="status">Only payments with this status (see <see cref="PaymentStatus"/>).</param>
        /// <param name="orderId">Only payments for this order.</param>
        /// <param name="cancellationToken">Cancels the requests.</param>
        public async IAsyncEnumerable<Payment> AllAsync(string? status = null, string? orderId = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string? cursor = null;
            do
            {
                var page = await ListAsync(status, orderId, cursor, cancellationToken).ConfigureAwait(false);
                foreach (var payment in page.Data)
                {
                    yield return payment;
                }
                cursor = page.Meta?.NextCursor;
            }
            while (!string.IsNullOrEmpty(cursor));
        }

        /// <summary>Cancels an open payment that has received nothing.</summary>
        /// <param name="paymentId">The payment ID.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Payment> CancelAsync(string paymentId, CancellationToken cancellationToken = default)
        {
            string path = "/payments/" + CoinoLiveClient.Id(paymentId, nameof(paymentId)) + "/cancel";
            return _client.DataAsync<Payment>(HttpMethod.Post, path, cancellationToken, repeatable: true);
        }
    }
}
