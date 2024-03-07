using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class CoinoLive
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public CoinoLive(string apiKey)
    {
        _httpClient = new HttpClient();
        _apiKey = apiKey;
    }

    public async Task<string> CreateOrder(double amount, string callbackUrl, string description, string gateId)
    {
        var payload = new
        {
            Amount = amount,
            Callback = callbackUrl,
            Description = description,
            Gate_ID = gateId,
            Create = true
        };

        var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);

        var response = await _httpClient.PostAsync("https://coino.live/api/v1/order", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public void RedirectPayment(string orderId)
    {
        var paymentUrl = "https://coino.live/orders/" + orderId;
        System.Diagnostics.Process.Start(paymentUrl);
    }

    public async Task<string> VerifyPayment(string orderId)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);

        var response = await _httpClient.GetAsync($"https://coino.live/api/v1/verify?order={orderId}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
