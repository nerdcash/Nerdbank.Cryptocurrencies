// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by a key that is implemented by the same class whether it functions
/// as a full or incoming viewing key.
/// </summary>
/// <remarks>
/// By implementing this interface, an auditing function can verify whether a given key instance
/// actually contains full viewing or incoming viewing capability.
/// </remarks>
internal interface IFullOrIncomingViewingKey : IFullViewingKey
{
	/// <summary>
	/// Gets a value indicating whether this key can observe details about outgoing transactions.
	/// </summary>
	bool IsFullViewingKey { get; }
}
