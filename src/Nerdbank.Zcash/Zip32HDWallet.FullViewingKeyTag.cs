// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using static Nerdbank.Bitcoin.Bip32HDWallet;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// The tag for a full viewing key (the first four bytes from its fingerprint). May not be unique.
	/// </summary>
	[InlineArray(Length)]
	public struct FullViewingKeyTag : IEquatable<FullViewingKeyTag>
	{
		/// <summary>
		/// The length of the value in bytes.
		/// </summary>
		public const int Length = 4;

		private byte element;

		/// <summary>
		/// Initializes a new instance of the <see cref="FullViewingKeyTag"/> struct.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		public FullViewingKeyTag(ReadOnlySpan<byte> value)
		{
			value.CopyToWithLengthCheck(this);
		}

		/// <summary>
		/// Gets this value as a <see cref="ParentFingerprint"/>.
		/// </summary>
		[UnscopedRef]
		public ref readonly ParentFingerprint AsParentFingerprint => ref ParentFingerprint.From(this[..]);

		/// <summary>
		/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		/// <returns>The strongly-typed element.</returns>
		public static ref readonly FullViewingKeyTag From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, FullViewingKeyTag>(value));

		/// <summary>
		/// Casts a <see cref="ParentFingerprint"/> to a <see cref="FullViewingKeyTag"/>.
		/// </summary>
		/// <param name="value">The fingerprint.</param>
		/// <returns>The full viewing key tag.</returns>
		public static ref readonly FullViewingKeyTag From(in ParentFingerprint value) => ref From(value[..]);

		/// <inheritdoc/>
		readonly bool IEquatable<FullViewingKeyTag>.Equals(FullViewingKeyTag other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in FullViewingKeyTag other) => this[..].SequenceEqual(other);
	}
}
