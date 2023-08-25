// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A spending key.
/// </summary>
public class SpendingKey : ISpendingKey
{
	private const string Bech32MainNetworkHRP = "secret-spending-key-main";
	private const string Bech32TestNetworkHRP = "secret-spending-key-test";

	private readonly Bytes32 secret;

	/// <summary>
	/// Initializes a new instance of the <see cref="SpendingKey"/> class.
	/// </summary>
	/// <param name="secret">The spending key.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal SpendingKey(ReadOnlySpan<byte> secret, ZcashNetwork network)
	{
		this.secret = new(secret);
		this.Network = network;
	}

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the diversifiable full viewing key.
	/// </summary>
	public DiversifiableFullViewingKey FullViewingKey => throw new NotImplementedException();

	/// <inheritdoc/>
	IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

	/// <summary>
	/// Gets the incoming viewing key.
	/// </summary>
	public IncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

	/// <summary>
	/// Gets the buffer. Always 32 bytes in length.
	/// </summary>
	internal ReadOnlySpan<byte> Secret => this.secret.Value;
}
