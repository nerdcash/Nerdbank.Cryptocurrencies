// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by Zcash spending keys.
/// </summary>
public interface ISpendingKey : IFullViewingKey
{
	/// <summary>
	/// Gets a key that can only view transactions (as opposed to being able to spend them).
	/// </summary>
	/// <remarks>
	/// Implementations should <em>not</em> return <c>this</c>, but rather a key that actually has fewer capabilities than the original object.
	/// </remarks>
	IFullViewingKey FullViewingKey { get; }

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;
}
