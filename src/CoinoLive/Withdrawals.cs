using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CoinoLive
{
    /// <summary>API withdrawals. Turn them on in the gateway settings first.</summary>
    public sealed class Withdrawals
    {
        private readonly CoinoLiveClient _client;

        internal Withdrawals(CoinoLiveClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Sends coins from your balance (or a custody user's balance) to an address.
        /// Give a reference: the same reference returns the same withdrawal, so a retried request never pays twice.
        /// </summary>
        /// <param name="parameters">The coin, amount, address and the optional fields.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Withdrawal> CreateAsync(CreateWithdrawalParams parameters, CancellationToken cancellationToken = default)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            return _client.DataAsync<Withdrawal>(HttpMethod.Post, "/withdrawals", cancellationToken, parameters,
                repeatable: !string.IsNullOrEmpty(parameters.Reference));
        }

        /// <summary>Reads a withdrawal.</summary>
        /// <param name="withdrawalId">The withdrawal ID.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Withdrawal> RetrieveAsync(string withdrawalId, CancellationToken cancellationToken = default)
        {
            return _client.DataAsync<Withdrawal>(HttpMethod.Get, "/withdrawals/" + CoinoLiveClient.Id(withdrawalId, nameof(withdrawalId)), cancellationToken);
        }
    }
}
