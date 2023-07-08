// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		/// <summary>
		/// A spending key.
		/// </summary>
		internal readonly struct SpendingKey
		{
			private readonly Bytes32 value;

			internal SpendingKey(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// Gets the spending key, broken out into 3 derived components.
		/// </summary>
		internal readonly struct ExpandedSpendingKey
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExpandedSpendingKey"/> struct.
			/// </summary>
			/// <param name="ask">The ask component.</param>
			/// <param name="nsk">The nsk component.</param>
			/// <param name="ovk">The outgoing viewing key.</param>
			internal ExpandedSpendingKey(ReadOnlySpan<byte> ask, ReadOnlySpan<byte> nsk, ReadOnlySpan<byte> ovk)
			{
				this.Ask = new(ask);
				this.Nsk = new(nsk);
				this.Ovk = new(ovk);
			}

			/// <summary>
			/// Gets the ask component of the spending key.
			/// </summary>
			internal Bytes32 Ask { get; }

			/// <summary>
			/// Gets the nsk component of the spending key.
			/// </summary>
			internal Bytes32 Nsk { get; }

			/// <summary>
			/// Gets the outgoing viewing key.
			/// </summary>
			internal OutgoingViewingKey Ovk { get; }

			/// <summary>
			/// Decodes an <see cref="ExpandedSpendingKey"/> from its binary representation.
			/// </summary>
			/// <param name="bytes">The buffer to read from. Must be at least 96 bytes.</param>
			/// <returns>The decoded spending key.</returns>
			internal static ExpandedSpendingKey FromBytes(ReadOnlySpan<byte> bytes) => new(bytes[0..32], bytes[32..64], bytes[64..96]);

			/// <summary>
			/// Encodes this instance into its binary representation.
			/// </summary>
			/// <param name="result">The buffer to write to. Must be at least 96 bytes.</param>
			/// <returns>The number of bytes written to <paramref name="result" />. Always 96.</returns>
			internal int ToBytes(Span<byte> result)
			{
				int written = 0;
				written += this.Ask.Value.CopyToRetLength(result[written..]);
				written += this.Nsk.Value.CopyToRetLength(result[written..]);
				written += this.Ovk.Value.CopyToRetLength(result[written..]);
				Assumes.True(written == 96);
				return written;
			}

			/// <summary>
			/// Encodes this instance to its binary representation.
			/// </summary>
			/// <returns>The serialized form.</returns>
			internal Bytes96 ToBytes()
			{
				Span<byte> bytes = stackalloc byte[96];
				this.ToBytes(bytes);
				return new(bytes);
			}
		}

		/// <summary>
		/// A diversifier key.
		/// </summary>
		internal readonly struct DiversifierKey
		{
			private readonly Bytes32 value;

			internal DiversifierKey(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// An outgoing viewing key.
		/// </summary>
		internal readonly struct OutgoingViewingKey
		{
			private readonly Bytes32 value;

			internal OutgoingViewingKey(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// A subgroup point.
		/// </summary>
		internal readonly struct SubgroupPoint
		{
			private readonly Bytes32 value;

			internal SubgroupPoint(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}
	}
}
