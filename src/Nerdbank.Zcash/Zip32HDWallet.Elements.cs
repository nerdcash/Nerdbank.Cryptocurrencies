// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// A chain code.
	/// </summary>
	public readonly struct ChainCode
	{
		private readonly Bytes32 value;

		internal ChainCode(ReadOnlySpan<byte> value)
		{
			this.value = new(value);
		}

		/// <summary>
		/// Gets the buffer. Always 32 bytes in length.
		/// </summary>
		internal readonly ReadOnlySpan<byte> Value => this.value.Value;
	}

	/// <summary>
	/// The fingerprint for a full viewing key. Guaranteed to be unique.
	/// </summary>
	public readonly struct FullViewingKeyFingerprint
	{
		private readonly Bytes32 value;

		internal FullViewingKeyFingerprint(ReadOnlySpan<byte> value)
		{
			this.value = new(value);
		}

		/// <summary>
		/// Gets the buffer. Always 32 bytes in length.
		/// </summary>
		internal readonly ReadOnlySpan<byte> Value => this.value.Value;
	}

	/// <summary>
	/// The tag for a full viewing key. May not be unique.
	/// </summary>
	public readonly struct FullViewingKeyTag
	{
		private readonly Bytes4 value;

		internal FullViewingKeyTag(ReadOnlySpan<byte> value)
		{
			this.value = new(value);
		}

		/// <summary>
		/// Gets the buffer. Always 4 bytes in length.
		/// </summary>
		internal readonly ReadOnlySpan<byte> Value => this.value.Value;
	}
}
