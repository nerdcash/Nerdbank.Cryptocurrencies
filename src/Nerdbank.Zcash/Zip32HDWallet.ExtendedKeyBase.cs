// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public abstract class ExtendedKeyBase : IExtendedKey
	{
		private const int ChainCodeLength = 32;
		private const int ParentFullViewingKeyTagLength = 4;
		private readonly FixedArrays fixedArrays;

		internal ExtendedKeyBase(ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
		{
			if (chainCode.Length != 32)
			{
				throw new ArgumentException($"Length must be exactly 32, but was {chainCode.Length}.", nameof(chainCode));
			}

			this.fixedArrays = new(chainCode, parentFullViewingKeyTag);
			this.Depth = depth;
			this.ChildNumber = childNumber;
			this.IsTestNet = isTestNet;
		}

		/// <summary>
		/// Gets a value indicating whether this key belongs to a TestNet (as opposed to a MainNet).
		/// </summary>
		public bool IsTestNet { get; }

		/// <summary>
		/// Gets the number of derivations from the master key to this one.
		/// </summary>
		public byte Depth { get; }

		/// <summary>
		/// Gets the index number used when deriving this key from its direct parent.
		/// </summary>
		public uint ChildNumber { get; }

		/// <summary>
		/// Gets the first 32-bits of the <see cref="Fingerprint"/> of the parent key.
		/// </summary>
		protected internal ReadOnlySpan<byte> ParentFullViewingKeyTag => this.fixedArrays.ParentFullViewingKeyTag;

		/// <summary>
		/// Gets the chain code for this key.
		/// </summary>
		protected internal ReadOnlySpan<byte> ChainCode => this.fixedArrays.ChainCode;

		/// <summary>
		/// Derives an extended key from a parent extended key.
		/// </summary>
		/// <param name="childNumber">The index of the derived child key.</param>
		/// <returns>The derived key.</returns>
		public abstract IExtendedKey Derive(uint childNumber);

		private unsafe struct FixedArrays
		{
			private fixed byte chainCode[ChainCodeLength];
			private fixed byte parentFingerprint[ParentFullViewingKeyTagLength];

			internal FixedArrays(ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag)
			{
				Requires.Argument(chainCode.Length == ChainCodeLength, nameof(chainCode), null);
				Requires.Argument(parentFullViewingKeyTag.Length is 0 or ParentFullViewingKeyTagLength, nameof(parentFullViewingKeyTag), null);

				chainCode.CopyTo(this.ChainCodeWritable);
				parentFullViewingKeyTag.CopyTo(this.ParentFullViewingKeyTagWritable);
			}

			internal readonly ReadOnlySpan<byte> ChainCode => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.chainCode[0]), ChainCodeLength);

			internal readonly ReadOnlySpan<byte> ParentFullViewingKeyTag => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.parentFingerprint[0]), ParentFullViewingKeyTagLength);

			private Span<byte> ChainCodeWritable => MemoryMarshal.CreateSpan(ref this.chainCode[0], ChainCodeLength);

			private Span<byte> ParentFullViewingKeyTagWritable => MemoryMarshal.CreateSpan(ref this.parentFingerprint[0], ParentFullViewingKeyTagLength);
		}
	}
}
