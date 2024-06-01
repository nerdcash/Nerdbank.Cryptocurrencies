// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by receivers that are embedded in Zcash addresses
/// that are allowed in unified addresses.
/// </summary>
public interface IUnifiedPoolReceiver : IPoolReceiver
{
	/// <summary>
	/// Gets the type code that identifies the type of receiver in a Unified Address.
	/// </summary>
	static abstract byte UnifiedReceiverTypeCode { get; }
}
