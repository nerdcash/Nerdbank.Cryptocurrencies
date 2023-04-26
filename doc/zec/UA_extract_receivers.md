# How to extract receivers from a Unified Address

A Unified Address (UA) may contain one or more "receivers".
Extracting these is necessary to transmit Zcash to the owner of the UA.

Your application or service may only support a subset of the possible receiver types.
Maybe your service does not yet support sending to the Orchard pool.
Or maybe you only send to transparent addresses today.

## Extracting a particular address type from a unified address

With the following code, you can extract the transparent address from a UA.
This allows you to start accepting Zcash NU5 upgrade UA addresses that include a transparent receiver.

```cs
static TransparentAddress? ExtractTransparentAddress(string address)
{
	if (!ZcashAddress.TryParse(address, out ZcashAddress? addr))
	{
		// Not a valid Zcash address.
		return null;
	}

	if (addr.Network != ZcashNetwork.MainNet)
	{
		// We are sending real ZEC, so only accept addresses on MainNet.
		return null;
	}

	return addr switch
	{
		TransparentAddress tAddr => tAddr,
		UnifiedAddress uAddr => uAddr.Receivers.OfType<TransparentAddress>().FirstOrDefault(),
		_ => null,
	};
}
```

You can then take this transparent address and use it in your existing payment system.

There are other address types of course.
You can check for `SaplingAddress` and `OrchardAddress` (or even the deprecated `SproutAddress`).

## Extracting the raw receivers for cryptographic parameters

If you're looking to get at the raw receiver and its cryptography parameters, you can do that too.
The following example demonstrates how you could extract the crypto parameters for multiple supported pools:

```cs
static bool TrySendZec(string address, decimal amount)
{
    if (!ZcashAddress.TryParse(address, out ZcashAddress? addr))
    {
        // Not a valid Zcash address.
        return false;
    }

    if (addr.Network != ZcashNetwork.MainNet)
    {
        // We are sending real ZEC, so only accept addresses on MainNet.
        return false;
    }

    if (addr.GetPoolReceiver<SaplingReceiver>() is { } sapling)
    {
        SendCryptoToSapling(sapling.D, sapling.Pkd, amount);
    }
    else if (addr.GetPoolReceiver<TransparentP2PKHReceiver>() is { } p2pkh)
    {
        SendCryptoToTransparent(p2pkh.ValidatingKeyHash, amount);
    }
    else
    {
        return false;
    }

    return true;
}
```

Note the `SendCryptoTo*` methods are beyond the scope of this example, and actually *sending* Zcash is beyond the capabilities of this library at present.
