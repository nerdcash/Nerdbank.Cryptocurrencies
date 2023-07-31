// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Zcash;

/// <summary>
/// Utility methods for unified address and viewing key encoding/decoding.
/// </summary>
internal static class UnifiedEncoding
{
	/// <summary>
	/// The length of the padding in a unified address.
	/// </summary>
	internal const int PaddingLength = 16;

	/// <summary>
	/// The shortest allowed length of the input to the <see cref="F4Jumble"/> function.
	/// </summary>
	internal const int MinF4JumbleInputLength = 48;

	/// <summary>
	/// The longest allowed length of the input to the <see cref="F4Jumble"/> function.
	/// </summary>
	internal const int MaxF4JumbleInputLength = 4194368;

	private const int F4OutputLength = 64; // known in the spec as ℒᵢ

	private static ReadOnlySpan<byte> StartingHPersonalization => new byte[] { 85, 65, 95, 70, 52, 74, 117, 109, 98, 108, 101, 95, 72, 0, 0, 0 };

	private static ReadOnlySpan<byte> StartingGPersonalization => new byte[] { 85, 65, 95, 70, 52, 74, 117, 109, 98, 108, 101, 95, 71, 0, 0, 0 };

	/// <summary>
	/// Applies the F4Jumble function to the specified buffer.
	/// </summary>
	/// <param name="ua">The buffer to mutate.</param>
	/// <param name="inverted"><see langword="true" /> to reverse the process.</param>
	/// <exception cref="ArgumentException">Thrown when the input buffer is shorter than <see cref="MinF4JumbleInputLength"/> or longer than <see cref="MaxF4JumbleInputLength"/>.</exception>
	/// <devremarks>
	/// <see href="https://docs.rs/f4jumble/latest/src/f4jumble/lib.rs.html#208">Some source for inspiration</see> while interpreting the spec.
	/// </devremarks>
	internal static void F4Jumble(Span<byte> ua, bool inverted = false)
	{
		if (ua.Length is < MinF4JumbleInputLength or > MaxF4JumbleInputLength)
		{
			throw new ArgumentException($"The UA cannot exceed {MaxF4JumbleInputLength} bytes.", nameof(ua));
		}

		Span<byte> arrayBuffer = stackalloc byte[ua.Length];
		ua.CopyTo(arrayBuffer);

		byte leftLength = (byte)Math.Min(F4OutputLength, ua.Length / 2);
		int rightLength = ua.Length - leftLength;
		Span<byte> hash = stackalloc byte[F4OutputLength];

		if (inverted)
		{
			RoundH(1, hash, arrayBuffer);
			RoundG(1, hash, arrayBuffer);
			RoundH(0, hash, arrayBuffer);
			RoundG(0, hash, arrayBuffer);
		}
		else
		{
			RoundG(0, hash, arrayBuffer);
			RoundH(0, hash, arrayBuffer);
			RoundG(1, hash, arrayBuffer);
			RoundH(1, hash, arrayBuffer);
		}

		arrayBuffer[..ua.Length].CopyTo(ua);

		void RoundG(byte i, Span<byte> hash, Span<byte> arrayBuffer)
		{
			Span<byte> personalization = stackalloc byte[StartingGPersonalization.Length];
			StartingGPersonalization.CopyTo(personalization);
			personalization[^3] = i;
			ushort top = checked((ushort)CeilDiv(rightLength, F4OutputLength));
			for (ushort j = 0; j < top; j++)
			{
				personalization[^2] = (byte)(j & 0xff);
				personalization[^1] = (byte)(j >> 8);
				Blake2B.Config config = new()
				{
					OutputSizeInBytes = F4OutputLength,
					Personalization = personalization,
				};
				int length = Blake2B.ComputeHash(arrayBuffer[..leftLength], hash, config);
				Xor(arrayBuffer[(leftLength + (j * F4OutputLength))..], hash[..length]);
			}
		}

		void RoundH(byte i, Span<byte> hash, Span<byte> arrayBuffer)
		{
			Span<byte> personalization = stackalloc byte[StartingHPersonalization.Length];
			StartingHPersonalization.CopyTo(personalization);
			personalization[^3] = i;
			Blake2B.Config config = new()
			{
				OutputSizeInBytes = leftLength,
				Personalization = personalization,
			};
			int length = Blake2B.ComputeHash(arrayBuffer.Slice(leftLength, rightLength), hash, config);
			Xor(arrayBuffer[..leftLength], hash[..length]);
		}

		static int CeilDiv(int number, int divisor) => (number + divisor - 1) / divisor;

		static void Xor(Span<byte> left, ReadOnlySpan<byte> right)
		{
			for (int i = 0; i < left.Length & i < right.Length; i++)
			{
				left[i] ^= right[i];
			}
		}
	}

	/// <summary>
	/// Initializes the padding buffer.
	/// </summary>
	/// <param name="humanReadablePart">The human readable part for the encoding.</param>
	/// <param name="padding">The buffer to write to. It must be at least 16 bytes.</param>
	/// <returns>The number of bytes written to <paramref name="padding"/>. Always 16.</returns>
	internal static int InitializePadding(ReadOnlySpan<char> humanReadablePart, Span<byte> padding)
	{
		int written = Encoding.UTF8.GetBytes(humanReadablePart, padding[0..PaddingLength]);
		padding[written..PaddingLength].Clear();
		return PaddingLength;
	}
}
