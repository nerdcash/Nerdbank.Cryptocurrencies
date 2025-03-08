// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Bitcoin;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// The first four bytes of the parent key's <see cref="ExtendedKeyBase.Identifier"/>.
	/// </summary>
	[InlineArray(Length)]
	public struct ParentFingerprint : IEquatable<ParentFingerprint>
	{
		/// <summary>
		/// The length of the value in bytes.
		/// </summary>
		public const int Length = 4;

		private byte element;

		/// <summary>
		/// Initializes a new instance of the <see cref="ParentFingerprint"/> struct.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		public ParentFingerprint(ReadOnlySpan<byte> value)
		{
			value.CopyToWithLengthCheck(this);
		}

		/// <summary>
		/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		/// <returns>The strongly-typed element.</returns>
		public static ref readonly ParentFingerprint From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ParentFingerprint>(value));

		/// <inheritdoc/>
		readonly bool IEquatable<ParentFingerprint>.Equals(ParentFingerprint other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in ParentFingerprint other) => this[..].SequenceEqual(other);
	}
}
