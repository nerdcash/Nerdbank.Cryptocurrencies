// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// An interface implemented by extended keys.
/// </summary>
public interface IExtendedKey : IKey
{
	/// <summary>
	/// Gets the index number used when deriving this key from its direct parent.
	/// </summary>
	uint ChildNumber { get; }

	/// <summary>
	/// Gets the derivation depth of the extended key.
	/// </summary>
	byte Depth { get; }

	/// <summary>
	/// Derives a new extended private key that is a direct child of this one.
	/// </summary>
	/// <param name="childNumber">The child key number to derive. This may include the <see cref="Bip32HDWallet.HardenedBit"/> to derive a hardened key.</param>
	/// <returns>A derived extended key.</returns>
	/// <exception cref="InvalidKeyException">
	/// Thrown in a statistically extremely unlikely event of the derived key being invalid.
	/// Callers should handle this exception by requesting a new key with an incremented value
	/// for <paramref name="childNumber"/>.
	/// </exception>
	/// <exception cref="NotSupportedException">Thrown if the value of the <see cref="Bip32HDWallet.HardenedBit"/> in the <paramref name="childNumber"/> argument is not supported by the receiving key.</exception>
	IExtendedKey Derive(uint childNumber);
}
