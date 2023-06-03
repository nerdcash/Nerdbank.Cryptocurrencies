// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Org.BouncyCastle.Crypto.Parameters;
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
		internal static readonly ECCurve Curve = new FpCurve(
			q: new("52435875175126190479447740508185965837690552500527637822603658699938581184513"),
			a: new("5"),
			b: new("1"),
			order: new("52435875175126190479447740508185965837690552500527637822603658699938581184512"),
			cofactor: new("1"));

		/// <summary>
		/// The basepoint (<c>G</c>) of the curve.
		/// </summary>
		internal static readonly ECPoint BasePoint = Curve.CreatePoint(
			new("35529392678556817526412750206378370998039579620092443977805913819117976852661"),
			new("17321585679444949914271250334039468343382271850312716954264829937000576695602"));

		private static readonly ECDomainParameters Parameters = new(Curve, BasePoint, Curve.Order);
	}

	/// <summary>
	/// The JubJub curve.
	/// </summary>
	/// <remarks>
	/// The equation is <c>au² + v² = 1 + du²v²</c>, where <c>a = -1</c> and <c>d = −10240/10241</c>.
	/// </remarks>
	internal static class JubJub
	{
		private const string r = "6554484396890773809930967563523245729705921265872317281365359162392183254199";
		private const string h = "8";

		internal static readonly System.Numerics.BigInteger Order = System.Numerics.BigInteger.Multiply(System.Numerics.BigInteger.Parse(h), System.Numerics.BigInteger.Parse(r));

		/// <summary>
		/// Derived from Cofactor * r or (h * r), which per the Zcash protocol defines Order of JubJub.
		/// </summary>
		private static readonly BigInteger n = new BigInteger(h).Multiply(new BigInteger(r));

		internal static readonly ECCurve Curve = new FpCurve(
			q: new("52435875175126190479447740508185965837690552500527637822603658699938581184513"),
			a: new("-1"),
			b: null, // ???
			order: n,
			cofactor: new(h));

		internal static readonly ECPoint BasePoint = null;

		private static readonly ECDomainParameters Parameters = new(Curve, BasePoint, Curve.Order);

		internal static ECPoint FindGroupHash(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2)
		{
			throw new NotImplementedException();
		}
	}
}
