// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// The tag for a full viewing key (the first four bytes from its fingerprint). May not be unique.
	/// </summary>
	public readonly struct FullViewingKeyTag
	{
		private readonly Bytes4 value;

		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeyTag"/> struct.
		/// </summary>
		/// <param name="value">The 4-byte tag.</param>
		public FullViewingKeyTag(ReadOnlySpan<byte> value)
		{
			this.value = new(value);
		}

		/// <summary>
		/// Gets the buffer. Always 4 bytes in length.
		/// </summary>
		[UnscopedRef]
		public readonly ReadOnlySpan<byte> Value => this.value.Value;
	}
}
