// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// The fingerprint for a full viewing key. Guaranteed to be unique.
	/// </summary>
	[InlineArray(Length)]
	public struct FullViewingKeyFingerprint : IEquatable<FullViewingKeyFingerprint>
	{
		/// <summary>
		/// The length of the value in bytes.
		/// </summary>
		public const int Length = 32;

		private byte element;

		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeyFingerprint"/> struct.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		public FullViewingKeyFingerprint(ReadOnlySpan<byte> value)
		{
			value.CopyToWithLengthCheck(this);
		}

		/// <summary>
		/// Gets the first 4 bytes of the fingerprint.
		/// </summary>
		[UnscopedRef]
		public ref readonly FullViewingKeyTag Tag => ref FullViewingKeyTag.From(this[..4]);

		/// <inheritdoc/>
		readonly bool IEquatable<FullViewingKeyFingerprint>.Equals(FullViewingKeyFingerprint other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in FullViewingKeyFingerprint other) => this[..].SequenceEqual(other);
	}
}
