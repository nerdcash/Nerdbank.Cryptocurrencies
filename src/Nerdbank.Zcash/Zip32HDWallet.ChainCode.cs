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

		/// <summary>
		/// Initializes a new instance of the <see cref="ChainCode"/> struct.
		/// </summary>
		/// <param name="value">The value of the buffer.</param>
		public ChainCode(ReadOnlySpan<byte> value)
		{
			this.value = new(value);
		}

		/// <summary>
		/// Gets the buffer. Always 32 bytes in length.
		/// </summary>
		public readonly ReadOnlySpan<byte> Value => this.value.Value;
	}
}
