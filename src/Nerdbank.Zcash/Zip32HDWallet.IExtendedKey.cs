// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// A base class for all extended keys.
	/// </summary>
	public interface IExtendedKey : Cryptocurrencies.IExtendedKey
	{
		/// <summary>
		/// Gets the first 32-bits of the fingerprint of the parent key.
		/// </summary>
		FullViewingKeyTag ParentFullViewingKeyTag { get; }

		/// <summary>
		/// Gets the chain code for this key.
		/// </summary>
		ChainCode ChainCode { get; }
	}
}
