// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
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
