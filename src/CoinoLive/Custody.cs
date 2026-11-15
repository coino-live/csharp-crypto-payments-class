using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CoinoLive
{
    /// <summary>Custody users: lifetime deposit addresses and balances for the users of your app.</summary>
    public sealed class Custody
    {
        private readonly CoinoLiveClient _client;

        internal Custody(CoinoLiveClient client)
        {
            _client = client;
        }

        /// <summary>Creates a user, or returns the existing one with the same external_id.</summary>
        /// <param name="parameters">The external_id and the optional fields.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<CustodyUser> CreateAsync(CreateCustodyUserParams parameters, CancellationToken cancellationToken = default)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            return _client.DataAsync<CustodyUser>(HttpMethod.Post, "/custody/users", cancellationToken, parameters, repeatable: true);
        }

        /// <summary>Reads a user, with addresses and balances.</summary>
        /// <param name="userId">The custody user ID.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<CustodyUser> RetrieveAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _client.DataAsync<CustodyUser>(HttpMethod.Get, "/custody/users/" + CoinoLiveClient.Id(userId, nameof(userId)), cancellationToken);
        }

        /// <summary>One page of users (50), newest first: <c>{ data, meta: { next_cursor, prev_cursor } }</c>.</summary>
        /// <param name="externalId">Only the user with this external_id.</param>
        /// <param name="cursor">A cursor from <see cref="PageMeta"/>.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<Page<CustodyUser>> ListAsync(string? externalId = null, string? cursor = null, CancellationToken cancellationToken = default)
        {
            string query = CoinoLiveClient.Query(("external_id", externalId), ("cursor", cursor));
            return _client.RequestAsync<Page<CustodyUser>>(HttpMethod.Get, "/custody/users" + query, null, null, false, cancellationToken);
        }

        /// <summary>One page of a user's credited deposits (50), newest first: <c>{ data, next_cursor }</c>.</summary>
        /// <param name="userId">The custody user ID.</param>
        /// <param name="cursor"><see cref="CustodyDepositPage.NextCursor"/> of the page before.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<CustodyDepositPage> DepositsAsync(string userId, string? cursor = null, CancellationToken cancellationToken = default)
        {
            string path = "/custody/users/" + CoinoLiveClient.Id(userId, nameof(userId)) + "/deposits" + CoinoLiveClient.Query(("cursor", cursor));
            return _client.RequestAsync<CustodyDepositPage>(HttpMethod.Get, path, null, null, false, cancellationToken);
        }

        /// <summary>Moves a user's balance of one asset to your main balance: all of it, or just amount.</summary>
        /// <param name="userId">The custody user ID.</param>
        /// <param name="asset">The coin, e.g. "usdt-tron".</param>
        /// <param name="amount">A decimal string in coins; null moves the whole balance.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        public Task<CustodyTransfer> TransferAsync(string userId, string asset, string? amount = null, CancellationToken cancellationToken = default)
        {
            string path = "/custody/users/" + CoinoLiveClient.Id(userId, nameof(userId)) + "/transfers";
            return _client.DataAsync<CustodyTransfer>(HttpMethod.Post, path, cancellationToken, new TransferBody { Asset = asset, Amount = amount });
        }

        private sealed class TransferBody
        {
            public string? Asset { get; set; }

            public string? Amount { get; set; }
        }
    }
}
