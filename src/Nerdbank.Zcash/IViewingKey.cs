// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by viewing keys.
/// </summary>
public interface IViewingKey : IKey
{
	/// <summary>
	/// Gets the network that this key operates on.
	/// </summary>
	ZcashNetwork Network { get; }

	/// <summary>
	/// Gets a value indicating whether this viewing key can see both incoming and outgoing transactions.
	/// </summary>
	bool IsFullViewingKey { get; }
}
