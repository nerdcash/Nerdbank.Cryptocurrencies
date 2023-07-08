// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key for incoming transactions.
/// </summary>
internal readonly struct ViewingKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ViewingKey"/> struct.
	/// </summary>
	/// <param name="ak">The ak value.</param>
	/// <param name="nk">The nk value.</param>
	internal ViewingKey(SubgroupPoint ak, NullifierDerivingKey nk)
	{
		this.Ak = ak;
		this.Nk = nk;
	}

	internal SubgroupPoint Ak { get; }

	internal NullifierDerivingKey Nk { get; }
}
