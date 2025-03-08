// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by incoming viewing keys.
/// </summary>
public interface IIncomingViewingKey : IZcashKey
{
	/// <summary>
	/// Gets the default receiving address associated with this key.
	/// </summary>
	ZcashAddress DefaultAddress { get; }
}
