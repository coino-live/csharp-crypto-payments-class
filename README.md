# CoinoLive Crypto Payment Gateway API Wrapper

This repository contains a C# class `CoinoLive` that provides functionality to interact with the CoinoLive Crypto Payment Gateway API. This class allows you to create orders, redirect payments, and verify payments easily within your C# applications.

## Installation

To use the `CoinoLive` class in your C# project, follow these steps:

1. Clone or download this repository.
2. Include the `CoinoLive.cs` file in your C# project.

## Usage

1. Initialize an instance of `CoinoLive` by providing your API key:

   ```csharp
   CoinoLive coinoLive = new CoinoLive("YOUR_API_KEY_HERE");
   ```

2. Use the `CreateOrder` method to create an order:

   ```csharp
   string response = await coinoLive.CreateOrder(10, "https://domain.com/verify_payment", "Pay For shoes", "M-2");
   ```

3. Redirect the user to the payment URL obtained from the `CreateOrder` method:

   ```csharp
   coinoLive.RedirectPayment("ORDER_ID");
   ```

4. Use the `VerifyPayment` method to verify the payment status by providing the order ID:

   ```csharp
   string verificationResponse = await coinoLive.VerifyPayment("ORDER_ID");
   ```

5. Handle the responses accordingly in your application.

## Dependencies

- Newtonsoft.Json (for JSON serialization/deserialization)

```

Make sure to replace `"YOUR_API_KEY_HERE"` with your actual CoinoLive API key when initializing the `CoinoLive` instance. Also, customize the usage instructions according to your specific implementation if needed.

```

## Connect with Us

Stay updated with the latest news and updates from Coino Live:

- Follow us on [Twitter](https://twitter.com/coino_live)
- Follow us on [Instagran](https://www.instagram.com/coino.live)
- Follow us on [Linkedin](https://linkedin.com/company/coino-live)
- Subscribe to our [Blog](https://coino.live/blog)
  
## Products

- [Accept Crypto Payments](https://coino.live/crypto-gateway)
- [Personal Crypto Gateway](https://coino.live/personal-crypto-gateway)
- [Crypto Donation](https://coino.live/crypto-donation)
- [Invoice Manager](https://coino.live/invoice-manager)
- [POS Crypto Payment Terminal](https://coino.live/point-of-sale)
- [Crypto Form Builder](https://coino.live/form-builder)

## Links

- [Website](https://coino.live)
- [About Us](https://coino.live/about)
- [Contact Us](https://coino.live/contact)
- [FAQ](https://coino.live/faq)


Thank you for choosing Coino Live for your cryptocurrency payment gateway needs!

