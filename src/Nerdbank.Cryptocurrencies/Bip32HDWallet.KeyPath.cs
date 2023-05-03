// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static System.FormattableString;

namespace Nerdbank.Cryptocurrencies;

public partial class Bip32HDWallet
{
	/// <summary>
	/// Represents a step in a path to a key.
	/// </summary>
	/// <param name="Index">The index for this particular step, including the <see cref="HardenedBit"/> if the key should be hardened.</param>
	/// <param name="Parent">The prior step in this path.</param>
	public record KeyPath(uint Index, KeyPath? Parent = null)
	{
		/// <summary>
		/// The bit that should be bitwise-OR'd with the <see cref="Index"/> to produce a hardened key.
		/// </summary>
		public const uint HardenedBit = 0x80000000;

		/// <summary>
		/// Gets a value indicating whether this key path should produce a hardened key.
		/// </summary>
		public bool IsHardened => (this.Index & 0x80000000) != 0;

		private string IndexWithApplicableHardenedFlag => this.IsHardened ? Invariant($"{this.Index & ~HardenedBit}'") : this.Index.ToString(CultureInfo.InvariantCulture);

		/// <summary>
		/// Parses an "m/1/2'/3" style string into a <see cref="KeyPath"/> instance.
		/// </summary>
		/// <param name="path">The key derivation path.</param>
		/// <returns>The parsed <see cref="KeyPath"/>.</returns>
		/// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is empty.</exception>
		/// <exception cref="FormatException">Thrown if the <paramref name="path"/> provided is not in the valid key derivation path format.</exception>
		public static KeyPath Parse(ReadOnlySpan<char> path)
		{
			if (!TryParse(path, out KeyPath? result))
			{
				throw new FormatException(Strings.InvalidBip32KeyPath);
			}

			return result;
		}

		/// <summary>
		/// Parses an "m/1/2'/3" style string into a <see cref="KeyPath"/> instance.
		/// </summary>
		/// <param name="path">The key derivation path.</param>
		/// <param name="result">Receives the parsed <see cref="KeyPath"/>. This <em>may</em> be non-<see langword="null" /> even when parsing ultimately fails, in which case it represents a partial result.</param>
		/// <returns><see langword="true" /> if the <paramref name="path"/> was valid and parsed; <see langword="false"/> otherwise.</returns>
		/// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is empty.</exception>
		public static bool TryParse(ReadOnlySpan<char> path, [NotNullWhen(true)] out KeyPath? result)
		{
			Requires.Argument(!path.IsEmpty, nameof(path), Strings.InvalidBip32KeyPath);

			result = null;
			if (path[0] != 'm')
			{
				return false;
			}

			ReadOnlySpan<char> remainingPath = path[1..];

			while (remainingPath.Length > 0)
			{
				if (remainingPath[0] != '/')
				{
					return false;
				}

				remainingPath = remainingPath[1..];
				int nextSlash = remainingPath.IndexOf('/');
				if (nextSlash < 0)
				{
					nextSlash = remainingPath.Length;
				}

				ReadOnlySpan<char> indexText = remainingPath[..nextSlash];
				if (indexText.Length == 0)
				{
					return false;
				}

				uint index;
				bool hardened = indexText[^1] == '\'';
				if (hardened)
				{
					indexText = indexText[..^1];
				}

				if (!uint.TryParse(indexText, CultureInfo.InvariantCulture, out index))
				{
					return false;
				}

				if (hardened)
				{
					index |= HardenedBit;
				}

				result = new KeyPath(index, result);
				remainingPath = remainingPath[nextSlash..];
			}

			return result is not null;
		}

		/// <summary>
		/// Prints out the standard "m/0/1'/2" format for the key path.
		/// </summary>
		/// <returns>A standard format "m/0/1/2" string.</returns>
		public override string ToString() => $"{this.Parent?.ToString() ?? "m"}/{this.IndexWithApplicableHardenedFlag}";
	}
}
