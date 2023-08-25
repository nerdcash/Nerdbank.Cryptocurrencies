// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

namespace Nerdbank.Zcash.Transparent;

/// <summary>
/// A private (spending) key for the transparent pool.
/// </summary>
public class P2PKHSpendingKey : ISpendingKey, IUnifiedEncodingElement
{
	/// <summary>
	/// Initializes a new instance of the <see cref="P2PKHSpendingKey"/> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="network">The network this key should be used with.</param>
	public P2PKHSpendingKey(ECPrivKey key, ZcashNetwork network)
	{
		Requires.NotNull(key);

		this.CryptographicKey = key;
		this.FullViewingKey = new(key.CreatePubKey(), network);
	}

	/// <summary>
	/// Gets the underlying cryptographic key.
	/// </summary>
	public ECPrivKey CryptographicKey { get; }

	/// <inheritdoc/>
	public ZcashNetwork Network => this.FullViewingKey.Network;

	/// <summary>
	/// Gets the public key.
	/// </summary>
	public P2PKHFullViewingKey FullViewingKey { get; }

	/// <inheritdoc/>
	IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

	/// <inheritdoc/>
	byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.TransparentP2PKH;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.UnifiedDataLength => 32;

	/// <inheritdoc/>
	int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
	{
		this.CryptographicKey.WriteToSpan(destination);
		return 32;
	}

	/// <inheritdoc cref="Zcash.Orchard.SpendingKey.DecodeUnifiedViewingKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
	internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
	{
		return new P2PKHSpendingKey(ECPrivKey.Create(keyContribution), network);
	}
}
