// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by Zcash spending keys.
/// </summary>
public interface ISpendingKey : IKey
{
	/// <summary>
	/// Gets the network that this key operates on.
	/// </summary>
	ZcashNetwork Network { get; }

	/// <inheritdoc/>
	bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;
}
