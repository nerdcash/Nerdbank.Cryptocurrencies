﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Bitcoin.Bip32HDWallet;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// A base class for all extended keys.
	/// </summary>
	public interface IExtendedKey : Cryptocurrencies.IExtendedKey, IZcashKey
	{
		/// <summary>
		/// Gets the key's fingerprint.
		/// </summary>
		ref readonly FullViewingKeyFingerprint Fingerprint { get; }

		/// <summary>
		/// Gets the first 32-bits of the fingerprint of the parent key.
		/// </summary>
		ref readonly FullViewingKeyTag ParentFullViewingKeyTag { get; }

		/// <summary>
		/// Gets the chain code for this key.
		/// </summary>
		ref readonly ChainCode ChainCode { get; }

		/// <inheritdoc cref="Cryptocurrencies.IExtendedKey"/>
		new IExtendedKey Derive(uint childIndex);
	}
}
