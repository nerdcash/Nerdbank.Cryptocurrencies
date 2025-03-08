// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static System.FormattableString;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Represents a step in a path to a key.
/// Each step links to its parent, forming a path.
/// </summary>
public record Bip32KeyPath : IComparable<Bip32KeyPath>
{
	/// <summary>
	/// The bit that should be bitwise-OR'd with the <see cref="Index"/> to produce a hardened key.
	/// </summary>
	public const uint HardenedBit = 0x80000000;

	/// <summary>
	/// The "m" root of the key path. Signifies the master key.
	/// </summary>
	public static readonly Bip32KeyPath Root = new();

	private readonly uint? index;

	/// <summary>
	/// Initializes a new instance of the <see cref="Bip32KeyPath"/> class.
	/// </summary>
	/// <param name="index">The index for this particular step, including the <see cref="HardenedBit"/> if the key should be hardened.</param>
	/// <param name="parent">
	/// The prior step in this path.
	/// May be <see langword="null" /> to indicate this is an unrooted path.
	/// If this is the first step in a (rooted) path, use <see cref="Root"/> here.
	/// </param>
	public Bip32KeyPath(uint index, Bip32KeyPath? parent = null)
	{
		this.index = index;
		this.Parent = parent;
	}

	private Bip32KeyPath()
	{
	}

	/// <summary>
	/// Gets the index for this particular step, including the <see cref="HardenedBit"/> if the key should be hardened.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when called on the <see cref="Root"/> instance.</exception>
	public uint Index => this.index ?? throw new InvalidOperationException(Strings.InvalidOnKeyPathRoot);

	/// <summary>
	/// Gets a value indicating whether this path is rooted.
	/// </summary>
	public bool IsRooted
	{
		get
		{
			Bip32KeyPath? current = this;
			while (current is not null)
			{
				if (current == Root)
				{
					return true;
				}

				current = current.Parent;
			}

			return false;
		}
	}

	/// <summary>
	/// Gets the prior step in this path.
	/// </summary>
	/// <value>This will be <see langword="null" /> for the <see cref="Root"/> instance or if this is the first element in an unrooted path.</value>
	public Bip32KeyPath? Parent { get; }

	/// <summary>
	/// Gets a value indicating whether this key path should produce a hardened key.
	/// </summary>
	public bool IsHardened => (this.Index & 0x80000000) != 0;

	/// <summary>
	/// Gets the number of steps in this path.
	/// </summary>
	/// <value>0 is for the <see cref="Root"/> path. Each derivation from that adds 1.</value>
	public uint Length => this.index is null ? 0 : ((this.Parent?.Length ?? 0) + 1);

	/// <summary>
	/// Gets a each step described by this <see cref="Bip32KeyPath" />.
	/// </summary>
	/// <returns>An enumeration of steps.</returns>
	public IEnumerable<Bip32KeyPath> Steps
	{
		get
		{
			for (uint i = 1; i <= this.Length; i++)
			{
				yield return this.Truncate(i);
			}
		}
	}

	private string IndexWithApplicableHardenedFlag => this.IsHardened ? Invariant($"{this.Index & ~HardenedBit}'") : this.Index.ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// Gets the index in this <see cref="Bip32KeyPath"/> at a particular position in the path.
	/// </summary>
	/// <param name="level">The position in the path, considering the first index to be 1.</param>
	/// <returns>The index at the specified path.</returns>
	public uint this[uint level]
	{
		get
		{
			// level 0 is the root ("m") which has no index.
			if (level <= 0)
			{
				throw new IndexOutOfRangeException();
			}

			if (level < this.Length)
			{
				return this.Parent?[level] ?? throw new IndexOutOfRangeException();
			}
			else if (level > this.Length)
			{
				throw new IndexOutOfRangeException();
			}
			else
			{
				return this.Index;
			}
		}
	}

	/// <summary>
	/// Parses an "m/1/2'/3" style string into a <see cref="Bip32KeyPath"/> instance.
	/// </summary>
	/// <param name="path">The key derivation path. Start with <c>m</c> to signify a rooted path, or omit this and start with a <c>/</c> to signify an unrooted (relative) path.</param>
	/// <returns>The parsed <see cref="Bip32KeyPath"/>.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is empty.</exception>
	/// <exception cref="FormatException">Thrown if the <paramref name="path"/> provided is not in the valid key derivation path format.</exception>
	public static Bip32KeyPath Parse(ReadOnlySpan<char> path)
	{
		if (!TryParse(path, out Bip32KeyPath? result))
		{
			throw new FormatException(Strings.InvalidBip32KeyPath);
		}

		return result;
	}

	/// <summary>
	/// Parses an "m/1/2'/3" style string into a <see cref="Bip32KeyPath"/> instance.
	/// </summary>
	/// <param name="path">The key derivation path.</param>
	/// <param name="result">Receives the parsed <see cref="Bip32KeyPath"/>. This <em>may</em> be non-<see langword="null" /> even when parsing ultimately fails, in which case it represents a partial result.</param>
	/// <returns><see langword="true" /> if the <paramref name="path"/> was valid and parsed; <see langword="false"/> otherwise.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is empty.</exception>
	public static bool TryParse(ReadOnlySpan<char> path, [NotNullWhen(true)] out Bip32KeyPath? result)
	{
		Requires.Argument(!path.IsEmpty, nameof(path), Strings.InvalidBip32KeyPath);

		result = null;
		ReadOnlySpan<char> remainingPath = path;

		if (path[0] == 'm')
		{
			result = Root;
			remainingPath = remainingPath[1..];
		}

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

			result = new Bip32KeyPath(index, result);
			remainingPath = remainingPath[nextSlash..];
		}

		return result is not null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Bip32KeyPath"/> class
	/// based on this one, but with one derivation step added to the end.
	/// </summary>
	/// <param name="index">The derivation step to add.</param>
	/// <returns>The new key path.</returns>
	public Bip32KeyPath Append(uint index) => new(index, this);

	/// <inheritdoc/>
	public int CompareTo(Bip32KeyPath? other)
	{
		if (other is null)
		{
			return 1;
		}

		// Compare all levels that have counterparts.
		uint greatestCommonLength = Math.Min(this.Length, other.Length);
		for (uint level = 1; level <= greatestCommonLength; level++)
		{
			int compare = CompareIndex(this[level], other[level]);
			if (compare != 0)
			{
				return compare;
			}
		}

		// If all levels are equal, then the longer path is greater.
		return this.Length.CompareTo(other.Length);

		static int CompareIndex(uint a, uint b)
		{
			// When comparing indexes, remove the hardened bit first so that 3 and 3' sort together.
			int compare = (a & ~HardenedBit).CompareTo(b & ~HardenedBit);
			if (compare != 0)
			{
				return compare;
			}

			// If the indexes are the same, then the hardened bit determines the sort order.
			if ((a & HardenedBit) != (b & HardenedBit))
			{
				return (a & HardenedBit) != 0 ? -1 : 1;
			}

			return 0;
		}
	}

	/// <summary>
	/// Prints out the standard "m/0/1'/2" format for the key path.
	/// </summary>
	/// <returns>A standard format "m/0/1/2" string.</returns>
	public override string ToString() => this == Root ? "m" : $"{this.Parent}/{this.IndexWithApplicableHardenedFlag}";

	/// <summary>
	/// Gets this <see cref="Bip32KeyPath"/> or some parent of it whose <see cref="Length"/> matches the specified <paramref name="length"/>.
	/// </summary>
	/// <param name="length">The desired length of the key path.</param>
	/// <returns>The key path.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the desired level is greater than this key path's own length.</exception>
	public Bip32KeyPath Truncate(uint length)
	{
		if (length < this.Length)
		{
			return this.Parent?.Truncate(length) ?? throw new ArgumentOutOfRangeException(nameof(length));
		}
		else if (length > this.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}
		else
		{
			return this;
		}
	}
}
