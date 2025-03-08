// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Bitcoin.Bip32HDWallet;

public class ExtendedPublicKeyTests : Bip32HDWalletTestBase
{
	public ExtendedPublicKeyTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	/// <summary>
	/// Asserts matching <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki#test-vectors">Test vectors from BIP-32</see>
	/// using the public key as the source of derivation wherever allowed.
	/// </summary>
	/// <param name="vector">The vector to test.</param>
	[Theory, MemberData(nameof(TestVectors))]
	public void Derive_TestVectors(TestVector vector)
	{
		ExtendedPrivateKey m = ExtendedPrivateKey.Create(Convert.FromHexString(vector.SeedAsHex));
		ExtendedPublicKey current = m.PublicKey;
		Bip32KeyPath keyPath = Bip32KeyPath.Root;
		this.logger.WriteLine($"Initial state: {keyPath}");
		TestVectorStep step = vector.Steps[0];
		AssertMatch();

		for (int i = 1; i < vector.Steps.Length; i++)
		{
			step = vector.Steps[i];
			keyPath = new Bip32KeyPath(step.ChildIndex, keyPath);
			if (keyPath.IsHardened)
			{
				this.logger.WriteLine($"Step {i}: {keyPath} (skipped due to hardened child)");

				// Continue the derivation by way of the private key so wecan take the next step.
				current = m.Derive(keyPath).PublicKey;
				AssertMatch();
			}
			else
			{
				this.logger.WriteLine($"Step {i}: {keyPath}");
				current = current.Derive(step.ChildIndex);
				AssertMatch();
			}
		}

		void AssertMatch()
		{
			AssertEqual(step.EncodedPublicKey, current);
		}
	}

	[Fact]
	public void Derive_Hardened()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		Assert.Throws<NotSupportedException>(() => master.PublicKey.Derive(2 | Bip32KeyPath.HardenedBit));
	}

	[Fact]
	public void Derive_KeyPath_FromMaster()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		string expected = master.PublicKey.Derive(1).Derive(2).Derive(3).ToString();

		ExtendedPublicKey derive12 = master.PublicKey.Derive(Bip32KeyPath.Parse("/1/2"));
		ExtendedPublicKey derive3 = derive12.Derive(Bip32KeyPath.Parse("/3"));
		AssertEqual(expected, derive3);

		ExtendedPublicKey derive123Unrooted = master.PublicKey.Derive(Bip32KeyPath.Parse("/1/2/3"));
		ExtendedPublicKey derive123Rooted = master.PublicKey.Derive(Bip32KeyPath.Parse("m/1/2/3"));
		AssertEqual(expected, derive123Unrooted);
		AssertEqual(expected, derive123Rooted);
	}

	[Fact]
	public void Derive_KeyPath_RootedOnNonMaster()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		ExtendedPublicKey derived = master.PublicKey.Derive(1);

		// This first simply cannot happen because one cannot derive a sibling key.
		Assert.Throws<NotSupportedException>(() => derived.Derive(Bip32KeyPath.Parse("m/2")));

		// This cannot happen until we decide that since we can match the depth and child number,
		// we can *assume* the rest of the path is the same and proceed to derive the next step.
		Assert.Throws<NotSupportedException>(() => derived.Derive(Bip32KeyPath.Parse("m/1/2")));
	}

	[Fact]
	public void Decode_Roundtripping()
	{
		using ExtendedPrivateKey pvk = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(128));
		ExtendedPublicKey pub = pvk.PublicKey;
		string pubAsString = pub.ToString();
		ExtendedPublicKey pub2 = Assert.IsType<ExtendedPublicKey>(ExtendedKeyBase.Decode(pub.ToString()));
		AssertEqual(pubAsString, pub2);

		// Test parsing of derived keys.
		ExtendedPublicKey pubDerived = pub.Derive(1);
		string pubDerivedAsString = pubDerived.ToString();
		ExtendedPublicKey pubDerived2 = Assert.IsType<ExtendedPublicKey>(ExtendedKeyBase.Decode(pubDerived.ToString()));
		AssertEqual(pubDerivedAsString, pubDerived2);
	}

	[Fact]
	public void DerivationPath()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		Assert.Equal(Bip32KeyPath.Root, master.DerivationPath);
		Assert.Equal("m/2", master.Derive(2).DerivationPath?.ToString());
	}
}
