// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public static partial class Orchard
	{
		internal static ExtendedSpendingKey GenerateMasterKey(ReadOnlySpan<byte> s, bool isTestNet = false)
		{
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(s, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Orchard"u8, OutputSizeInBytes = blakeOutput.Length });

			Span<byte> spendingKey = blakeOutput[..32];
			Span<byte> chainCode = blakeOutput[32..];

			SpendingKey key = new(spendingKey);
			return new ExtendedSpendingKey(
				key,
				chainCode,
				parentFullViewingKeyTag: default,
				depth: 0,
				childNumber: 0,
				isTestNet);
		}
	}
}
