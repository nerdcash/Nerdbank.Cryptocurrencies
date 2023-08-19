// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// The fingerprint for a full viewing key. Guaranteed to be unique.
	/// </summary>
	public readonly struct FullViewingKeyFingerprint : IEquatable<FullViewingKeyFingerprint>
	{
		private readonly Bytes32 value;

		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeyFingerprint"/> struct.
		/// </summary>
		/// <param name="value">The 32-byte fingerprint.</param>
		public FullViewingKeyFingerprint(ReadOnlySpan<byte> value)
		{
			this.value = new(value);
		}

		/// <summary>
		/// Gets the buffer. Always 32 bytes in length.
		/// </summary>
		public readonly ReadOnlySpan<byte> Value => this.value.Value;

		/// <summary>
		/// Gets the first 4 bytes of the fingerprint.
		/// </summary>
		public readonly FullViewingKeyTag Tag => new(this.value.Value[..4]);

		/// <inheritdoc/>
		public bool Equals(FullViewingKeyFingerprint other) => this.Value.SequenceEqual(other.Value);
	}
}
