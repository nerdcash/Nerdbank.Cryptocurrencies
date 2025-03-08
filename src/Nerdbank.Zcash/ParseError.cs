// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Enumerates the kinds of errors that can occur when parsing an address or URI.
/// </summary>
public enum ParseError
{
	/// <summary>
	/// The address did not conform to a recognized type.
	/// </summary>
	UnrecognizedAddressType,

	/// <summary>
	/// The address violated an internal consistency check.
	/// </summary>
	InvalidAddress,

	/// <summary>
	/// The URI had an unexpected <see cref="Uri.Scheme"/>.
	/// </summary>
	UnrecognizedScheme,

	/// <summary>
	/// The string is not a well-formed URI.
	/// </summary>
	InvalidUri,

	/// <summary>
	/// A URI has an invalid parameter.
	/// </summary>
	InvalidParam,

	/// <summary>
	/// A URI is missing a required parameter.
	/// </summary>
	MissingRequiredParameter,

	/// <summary>
	/// A URI included a parameter that MUST be recognized but was not.
	/// </summary>
	UnrecognizedRequiredParameter,
}
