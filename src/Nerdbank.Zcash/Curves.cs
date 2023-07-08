// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

/// <summary>
/// Elliptic curves used in Zcash cryptography.
/// </summary>
internal static class Curves
{
	/// <summary>
	/// The Pallas elliptic curve.
	/// </summary>
	/// <remarks>
	/// The equation is <c>y² = x³ + ax + b</c>.
	/// </remarks>
	internal static class Pallas
	{
		/// <summary>
		/// The curve.
		/// </summary>
		internal static readonly FpCurve Curve = new FpCurve(
			q: new("40000000000000000000000000000000224698fc094cf91b992d30ed00000001", 16),
			a: BigInteger.Zero,
			b: new("5"),
			order: new("40000000000000000000000000000000224698fc0994a8dd8c46eb2100000001", 16),
			cofactor: new("1"));

		/// <summary>
		/// The basepoint (<c>G</c>) of the curve.
		/// </summary>
		internal static readonly ECPoint BasePoint = Curve.CreatePoint(
			new("35529392678556817526412750206378370998039579620092443977805913819117976852661"),
			new("17321585679444949914271250334039468343382271850312716954264829937000576695602"));

		////private static readonly ECDomainParameters Parameters = new(Curve, BasePoint, Curve.Order);
	}
}
