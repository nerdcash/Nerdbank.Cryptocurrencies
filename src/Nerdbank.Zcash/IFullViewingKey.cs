// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by full viewing keys (i.e. viewing keys that can read incoming and outgoing transactions).
/// </summary>
public interface IFullViewingKey : IIncomingViewingKey
{
	/// <summary>
	/// Gets a key that can only read incoming transactions (as opposed to also being able to read outgoing transactions.)
	/// </summary>
	/// <remarks>
	/// Implementations should <em>not</em> return <c>this</c>, but rather a key that actually has fewer capabilities than the original object.
	/// </remarks>
	IIncomingViewingKey IncomingViewingKey { get; }

	/// <inheritdoc/>
	ZcashNetwork IZcashKey.Network => this.IncomingViewingKey.Network;

	/// <inheritdoc/>
	ZcashAddress IIncomingViewingKey.DefaultAddress => this.IncomingViewingKey.DefaultAddress;
}
