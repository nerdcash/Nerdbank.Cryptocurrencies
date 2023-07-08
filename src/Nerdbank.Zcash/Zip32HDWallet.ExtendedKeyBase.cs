// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// A base class for all extended keys.
	/// </summary>
	public abstract class ExtendedKeyBase : IExtendedKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedKeyBase"/> class.
		/// </summary>
		/// <param name="chainCode">The chain code.</param>
		/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
		/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
		/// <param name="childNumber">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
		/// <param name="isTestNet">A value indicating whether this key is to be used on a testnet.</param>
		internal ExtendedKeyBase(in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
		{
			this.ChainCode = chainCode;
			this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
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
		/// Gets the first 32-bits of the fingerprint of the parent key.
		/// </summary>
		protected internal FullViewingKeyTag ParentFullViewingKeyTag { get; }

		/// <summary>
		/// Gets the chain code for this key.
		/// </summary>
		protected internal ChainCode ChainCode { get; }

		/// <summary>
		/// Derives an extended key from a parent extended key.
		/// </summary>
		/// <param name="childNumber">The index of the derived child key.</param>
		/// <returns>The derived key.</returns>
		public abstract IExtendedKey Derive(uint childNumber);
	}
}
