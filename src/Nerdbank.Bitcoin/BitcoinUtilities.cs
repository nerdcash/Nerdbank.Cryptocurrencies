// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Bitcoin;

/// <summary>
/// Cryptocurrency utility and extension methods.
/// </summary>
public static class BitcoinUtilities
{
	/// <summary>
	/// Derives a new extended private key by following the steps in the specified path.
	/// </summary>
	/// <typeparam name="TKey">The type of extended key being derived.</typeparam>
	/// <param name="key">The parent key.</param>
	/// <param name="keyPath">The derivation path to follow to produce the new key.</param>
	/// <returns>A derived extended private key.</returns>
	/// <exception cref="InvalidKeyException">
	/// Thrown in a statistically extremely unlikely event of the derived key being invalid.
	/// Callers should handle this exception by requesting a new key with an incremented value
	/// for the child number at the failing position in the key path.
	/// </exception>
	public static TKey Derive<TKey>(this TKey key, Bip32KeyPath keyPath)
		where TKey : class, IExtendedKey
	{
		Requires.NotNull(key);
		Requires.NotNull(keyPath);

		if (key.Depth > 0 && keyPath.IsRooted)
		{
			throw new NotSupportedException("Deriving with a rooted key path from a non-rooted key is not supported.");
		}

		TKey result = key;
		TKey? intermediate = null;
		foreach (Bip32KeyPath step in keyPath.Steps)
		{
			try
			{
				result = (TKey)result.Derive(step.Index);

				// If this isn't our first time around, dispose of the previous intermediate key,
				// taking care to not dispose of the original key.
				(intermediate as IDisposable)?.Dispose();
				intermediate = result;
			}
			catch (InvalidKeyException ex)
			{
				throw new InvalidKeyException(Strings.FormatVeryUnlikelyUnvalidChildKeyOnPath(step), ex) { KeyPath = step };
			}
		}

		return result;
	}

	/// <summary>
	/// Returns a bitmask that exposes the specified number of bits in a byte, starting with the most significant bit.
	/// </summary>
	/// <param name="msbBits">The number of bits to expose.</param>
	/// <returns>The bitmask.</returns>
	internal static byte MaskMSB(int msbBits) => (byte)~MaskLSB(8 - msbBits);

	/// <summary>
	/// Returns a bitmask that exposes the specified number of bits in a byte, starting with the least significant bit.
	/// </summary>
	/// <param name="lsbBits">The number of bits to expose.</param>
	/// <returns>The bitmask.</returns>
	internal static byte MaskLSB(int lsbBits)
	{
		Requires.Range(lsbBits >= 0 && lsbBits <= 8, nameof(lsbBits));
		return (byte)((1 << lsbBits) - 1);
	}
}
