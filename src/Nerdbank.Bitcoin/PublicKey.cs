// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using NBitcoin.Secp256k1;

namespace Nerdbank.Bitcoin;

/// <summary>
/// An elliptic curve public key.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class PublicKey : IKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey" /> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="isTestNet">A value indicating whether this key operates on the testnet.</param>
	public PublicKey(ECPubKey key, bool isTestNet)
	{
		this.CryptographicKey = key;
		this.IsTestNet = isTestNet;

		this.P2PKHAddress = new(key, isTestNet);
	}

	/// <inheritdoc/>
	public bool IsTestNet { get; }

	/// <summary>
	/// Gets the Bitcoin address associated with this public key.
	/// </summary>
	public BitcoinP2PKHAddress P2PKHAddress { get; }

	/// <summary>
	/// Gets the underlying cryptographic key.
	/// </summary>
	public ECPubKey CryptographicKey { get; }

	private string DebuggerDisplay => this.P2PKHAddress.TextEncoding;
}
