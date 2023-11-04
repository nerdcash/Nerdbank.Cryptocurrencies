// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class RawTransactionTests : TestBase
{
	/// <summary>
	/// Verifies decoding of <see href="https://zcashblockexplorer.com/transactions/d8dc083c3143e73fd9879c3a69be668bee416f7df23d02c81ec0809b79633284">a transaction</see> that unshields from sapling
	/// into many transparent outputs.
	/// </summary>
	[Fact]
	public void Decode_SaplingUnshielding_v5()
	{
		const string rawTxBase64 = "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";
		RawTransaction tx = RawTransaction.Decode(Convert.FromBase64String(rawTxBase64));

		Assert.Equal(5u, tx.Version);
		Assert.True(tx.Overwintered);

		Assert.Equal(0, tx.Transparent.Inputs.Length);
		Assert.Equal(48, tx.Transparent.Outputs.Length);
		Assert.Equal(54, tx.Transparent.Outputs.Span[0].Value);

		Assert.Equal(default, tx.Sprout.JoinSplitGroth16);
		Assert.Equal(default, tx.Sprout.JoinSplitBCTV14);
		Assert.Equal(0, tx.Sprout.JoinSplitPublicKey.Length);
		Assert.Equal(0, tx.Sprout.JoinSplitSig.Length);

		Assert.Equal(5000, tx.Sapling.ValueBalance);
		Assert.Equal(1, tx.Sapling.Spends.Count);
		TestDescriptionEnumerator(tx.Sapling.Spends);
		Assert.Equal(2, tx.Sapling.Outputs.Count);
		TestDescriptionEnumerator(tx.Sapling.Outputs);

		Assert.Equal(0, tx.Orchard.ValueBalance);
		Assert.Equal(0, tx.Orchard.Actions.Count);
		Assert.Equal(0, tx.Orchard.BindingSig.Length);
		Assert.Equal(0, tx.Orchard.SpendAuthSigs.Length);
		Assert.Equal(0, tx.Orchard.Proofs.Length);
		Assert.Equal(0, tx.Orchard.Anchor.Length);
		Assert.Equal(default, tx.Orchard.Flags);
	}

	[Fact]
	public void Decode_SproutUnshielding_v2()
	{
		const string rawTxBase64 = "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";
		RawTransaction tx = RawTransaction.Decode(Convert.FromBase64String(rawTxBase64));

		Assert.Equal(2u, tx.Version);
		Assert.False(tx.Overwintered);

		Assert.Equal(0, tx.Transparent.Inputs.Length);
		Assert.Equal(1, tx.Transparent.Outputs.Length);
		Assert.Equal(548811, tx.Transparent.Outputs.Span[0].Value);

		Assert.Equal(default, tx.Sprout.JoinSplitGroth16);
		Assert.Equal(1, tx.Sprout.JoinSplitBCTV14.Length);
		Assert.Equal(32, tx.Sprout.JoinSplitPublicKey.Length);
		Assert.Equal(64, tx.Sprout.JoinSplitSig.Length);

		Assert.Equal(0, tx.Sapling.ValueBalance);
		Assert.Equal(0, tx.Sapling.Spends.Count);
		Assert.Equal(0, tx.Sapling.Outputs.Count);

		Assert.Equal(0, tx.Orchard.ValueBalance);
		Assert.Equal(0, tx.Orchard.Actions.Count);
		Assert.Equal(0, tx.Orchard.BindingSig.Length);
		Assert.Equal(0, tx.Orchard.SpendAuthSigs.Length);
		Assert.Equal(0, tx.Orchard.Proofs.Length);
		Assert.Equal(0, tx.Orchard.Anchor.Length);
		Assert.Equal(default, tx.Orchard.Flags);
	}

	/// <summary>
	/// Verifies decoding of <see href="https://zcashblockexplorer.com/transactions/384e1cf37736db74b37128c450ed9e023853effbf6e52ca4f04113ae21880ad4">a coinbase transaction</see>
	/// into many transparent outputs.
	/// </summary>
	[Fact]
	public void Decode_CoinbaseToTransparent_v4()
	{
		const string rawTxBase64 = "BAAAgIUgL4kBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/////BQO+BSIA/////wTou+YOAAAAABl2qRS6kv8GCB1f9lQq+NOy0gnSm6YzfIisQHh9AQAAAAAXqRSTH+xUwf6obldEYswyAT9UALiRKYc4yU0BAAAAABepFMekKF7XrteNjA4o1/GDnMtARqsMhyhr7gAAAAAAF6kU1Fyxrf+1IVpCcgUyoHbwLHx3jJCHAAAAAL4FIgAAAAAAAAAAAAAAAA==";
		RawTransaction tx = RawTransaction.Decode(Convert.FromBase64String(rawTxBase64));

		Assert.Equal(4u, tx.Version);
		Assert.True(tx.Overwintered);

		Assert.Equal(1, tx.Transparent.Inputs.Length);
		Assert.Equal(4, tx.Transparent.Outputs.Length);
		Assert.Equal(250002408, tx.Transparent.Outputs.Span[0].Value);

		Assert.Equal(default, tx.Sprout.JoinSplitGroth16);
		Assert.Equal(default, tx.Sprout.JoinSplitBCTV14);
		Assert.Equal(0, tx.Sprout.JoinSplitPublicKey.Length);
		Assert.Equal(0, tx.Sprout.JoinSplitSig.Length);

		Assert.Equal(0, tx.Sapling.ValueBalance);
		Assert.Equal(0, tx.Sapling.Spends.Count);
		TestDescriptionEnumerator(tx.Sapling.Spends);
		Assert.Equal(0, tx.Sapling.Outputs.Count);

		Assert.Equal(0, tx.Orchard.ValueBalance);
		Assert.Equal(0, tx.Orchard.Actions.Count);
		Assert.Equal(0, tx.Orchard.BindingSig.Length);
		Assert.Equal(0, tx.Orchard.SpendAuthSigs.Length);
		Assert.Equal(0, tx.Orchard.Proofs.Length);
		Assert.Equal(0, tx.Orchard.Anchor.Length);
		Assert.Equal(default, tx.Orchard.Flags);
	}

	/// <summary>
	/// Verifies decoding of <see href="https://zcashblockexplorer.com/transactions/851bf6fbf7a976327817c738c489d7fa657752445430922d94c983c0b9ed4609">a coinbase transaction</see>
	/// into many transparent outputs.
	/// </summary>
	[Fact]
	public void Decode_CoinbaseToTransparent_v1()
	{
		const string rawTxBase64 = "AQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP////8CUQD/////AlDDAAAAAAAAIyECekbrUTWIsBs36iQwP0tiiv0SzCDfeJ/t4JIeQ8rT6HWs1DAAAAAAAAAXqRR9Rqcw0x+XsZMNM2ipZ8MJvU0TaocAAAAA";
		RawTransaction tx = RawTransaction.Decode(Convert.FromBase64String(rawTxBase64));

		Assert.Equal(1u, tx.Version);
		Assert.False(tx.Overwintered);

		Assert.Equal(1, tx.Transparent.Inputs.Length);
		Assert.Equal(2, tx.Transparent.Outputs.Length);
		Assert.Equal(50000, tx.Transparent.Outputs.Span[0].Value);

		Assert.Equal(default, tx.Sprout.JoinSplitGroth16);
		Assert.Equal(default, tx.Sprout.JoinSplitBCTV14);
		Assert.Equal(0, tx.Sprout.JoinSplitPublicKey.Length);
		Assert.Equal(0, tx.Sprout.JoinSplitSig.Length);

		Assert.Equal(0, tx.Sapling.ValueBalance);
		Assert.Equal(0, tx.Sapling.Spends.Count);
		TestDescriptionEnumerator(tx.Sapling.Spends);
		Assert.Equal(0, tx.Sapling.Outputs.Count);

		Assert.Equal(0, tx.Orchard.ValueBalance);
		Assert.Equal(0, tx.Orchard.Actions.Count);
		Assert.Equal(0, tx.Orchard.BindingSig.Length);
		Assert.Equal(0, tx.Orchard.SpendAuthSigs.Length);
		Assert.Equal(0, tx.Orchard.Proofs.Length);
		Assert.Equal(0, tx.Orchard.Anchor.Length);
		Assert.Equal(default, tx.Orchard.Flags);
	}

	private static void TestDescriptionEnumerator<T>(RawTransaction.DescriptionEnumerator<T> enumerator)
		where T : struct
	{
		int count = enumerator.Count;
		List<T> list = new(count);
		int seen;
		for (seen = 0; seen < count; ++seen)
		{
			Assert.True(enumerator.MoveNext());
			list.Add(enumerator.Current);
		}

		Assert.False(enumerator.MoveNext());
		enumerator.Reset();
		for (seen = 0; seen < count; ++seen)
		{
			Assert.True(enumerator.MoveNext());
			Assert.Equal(list[seen], enumerator.Current);
		}

		Assert.False(enumerator.MoveNext());
	}
}
