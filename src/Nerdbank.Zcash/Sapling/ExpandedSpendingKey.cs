// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// Gets the spending key, broken out into 3 derived components.
/// </summary>
public class ExpandedSpendingKey : IEquatable<ExpandedSpendingKey>, ISpendingKey
{
	private readonly Bytes32 ask;
	private readonly Bytes32 nsk;
	private readonly OutgoingViewingKey ovk;
	private readonly DiversifierKey dk;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExpandedSpendingKey"/> class.
	/// </summary>
	/// <param name="ask">The ask component.</param>
	/// <param name="nsk">The nsk component.</param>
	/// <param name="ovk">The outgoing viewing key.</param>
	/// <param name="dk">The diversifier key, which allows for generating many addresses that send funds to the same spending authority.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal ExpandedSpendingKey(ReadOnlySpan<byte> ask, ReadOnlySpan<byte> nsk, ReadOnlySpan<byte> ovk, DiversifierKey dk, ZcashNetwork network)
	{
		this.ask = new(ask);
		this.nsk = new(nsk);
		this.ovk = new(ovk);
		this.dk = dk;

		this.FullViewingKey = new(
			Sapling.FullViewingKey.Create(ask, nsk, ovk, network),
			dk);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExpandedSpendingKey"/> class
	/// based on just the original 32-byte spending key.
	/// </summary>
	/// <param name="spendingKey">The 32-byte spending key.</param>
	/// <param name="network">The network this key should be used with.</param>
	internal ExpandedSpendingKey(ReadOnlySpan<byte> spendingKey, ZcashNetwork network)
	{
		Span<byte> expandedSpendingKey = stackalloc byte[96];
		NativeMethods.GetSaplingExpandedSpendingKey(spendingKey, expandedSpendingKey);
		this.ask = new(expandedSpendingKey[..32]);
		this.nsk = new(expandedSpendingKey[32..64]);

		Span<byte> expandOutput = stackalloc byte[64];
		ZcashUtilities.PRFexpand(spendingKey, PrfExpandCodes.SaplingOvk, expandOutput);
		this.ovk = new(expandOutput[..32]);

		ZcashUtilities.PRFexpand(spendingKey, PrfExpandCodes.SaplingDk, expandOutput);
		this.dk = new(expandOutput[..32]);
		this.FullViewingKey = new(
			Sapling.FullViewingKey.Create(this.Ask, this.Nsk, this.Ovk, network),
			this.Dk);
	}

	/// <inheritdoc/>
	public ZcashNetwork Network => this.FullViewingKey.Network;

	/// <inheritdoc/>
	IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

	/// <summary>
	/// Gets the incoming viewing key.
	/// </summary>
	public DiversifiableIncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

	/// <summary>
	/// Gets the spend authorization key.
	/// </summary>
	internal ref readonly Bytes32 Ask => ref this.ask;

	/// <summary>
	/// Gets the nsk component of the spending key.
	/// </summary>
	internal ref readonly Bytes32 Nsk => ref this.nsk;

	/// <summary>
	/// Gets the outgoing viewing key.
	/// </summary>
	internal ref readonly OutgoingViewingKey Ovk => ref this.ovk;

	/// <summary>
	/// Gets the diversifier key.
	/// </summary>
	internal ref readonly DiversifierKey Dk => ref this.dk;

	/// <summary>
	/// Gets the diversifiable full viewing key.
	/// </summary>
	internal DiversifiableFullViewingKey FullViewingKey { get; }

	/// <inheritdoc/>
	public bool Equals(ExpandedSpendingKey? other)
	{
		return other is not null
			&& this.Ask.Equals(other.Ask)
			&& this.Nsk.Equals(other.Nsk)
			&& this.Ovk.Equals(other.Ovk);
	}

	/// <summary>
	/// Decodes an <see cref="ExpandedSpendingKey"/> from its binary representation.
	/// </summary>
	/// <param name="bytes">The buffer to read from. Must be at least 96 bytes.</param>
	/// <param name="dk">The diversifier key.</param>
	/// <param name="network">The network this key should be used with.</param>
	/// <returns>The decoded spending key.</returns>
	internal static ExpandedSpendingKey FromBytes(ReadOnlySpan<byte> bytes, DiversifierKey dk, ZcashNetwork network)
	{
		return new(bytes[0..32], bytes[32..64], bytes[64..96], dk, network);
	}

	/// <summary>
	/// Encodes this instance into its binary representation.
	/// </summary>
	/// <param name="result">The buffer to write to. Must be at least 96 bytes.</param>
	/// <returns>The number of bytes written to <paramref name="result" />. Always 96.</returns>
	internal int ToBytes(Span<byte> result)
	{
		int written = 0;
		written += this.Ask[..].CopyToRetLength(result[written..]);
		written += this.Nsk[..].CopyToRetLength(result[written..]);
		written += this.Ovk[..].CopyToRetLength(result[written..]);
		Assumes.True(written == 96);
		return written;
	}

	/// <summary>
	/// Encodes this instance to its binary representation.
	/// </summary>
	/// <returns>The serialized form.</returns>
	internal Bytes96 ToBytes()
	{
		Bytes96 result = default;
		this.ToBytes(result);
		return result;
	}
}
