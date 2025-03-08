// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// An interface implemented by any cryptocurrency key.
/// </summary>
public interface IKey
{
	/// <summary>
	/// Gets a value indicating whether this key belongs to a TestNet (as opposed to a MainNet).
	/// </summary>
	bool IsTestNet { get; }
}
