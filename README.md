# CoinoLive Crypto Payment Gateway API Wrapper

This repository contains a C# class `CoinoLive` that provides functionality to interact with the CoinoLive Crypto Payment Gateway API. This class allows you to create orders, redirect payments, and verify payments easily within your C# applications.


## Installation

To integrate the CoinoLive API wrapper into your Rust project, follow these simple steps:

1. Clone or download this repository to your local machine.
2. Include the `coino_live.rs` file in your Rust project.

## Usage

Initialize an instance of CoinoLive by providing your API key:

```rust
let coino_live = CoinoLive::new("YOUR_API_KEY_HERE".to_string());
```

Use the `create_order` method to create an order:

```rust
let response = coino_live.create_order(10.0, "https://domain.com/verify_payment".to_string(), "Pay For shoes".to_string(), "M-2".to_string()).await?;
```

Redirect the user to the payment URL obtained from the `create_order` method:

```rust
coino_live.redirect_payment("ORDER_ID");
```

Use the `verify_payment` method to verify the payment status by providing the order ID:

```rust
let verification_response = coino_live.verify_payment("ORDER_ID").await?;
```

Handle the responses accordingly in your application.

## Dependencies

- reqwest (for making HTTP requests)
- serde (for JSON serialization/deserialization)

Make sure to replace `"YOUR_API_KEY_HERE"` with your actual CoinoLive API key when initializing the `CoinoLive` instance.

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
