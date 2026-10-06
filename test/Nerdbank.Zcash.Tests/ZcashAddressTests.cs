// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Bitcoin;
using static Nerdbank.Zcash.ZcashAddress.Match;

public class ZcashAddressTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public ZcashAddressTests()
	{
		this.logger = TestOutputHelper.Instance;
	}

	public static object[][] ValidAddresses => new object[][]
	{
		new object[] { ValidUnifiedAddressOrchardSapling },
		new object[] { ValidUnifiedAddressOrchard },
		new object[] { ValidSaplingAddress },
		new object[] { ValidSproutAddress },
		new object[] { ValidTransparentP2PKHAddress },
		new object[] { ValidTransparentP2SHAddress },
	};

	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { string.Empty },
		new object?[] { "foo" },
	};

	[Test, MethodDataSource(nameof(ValidAddresses))]
	public void Decode_Valid(string address)
	{
		var addr = ZcashAddress.Decode(address);
		Assert.Equal(address, addr.ToString());
	}

	[Test, MethodDataSource(nameof(ValidAddresses))]
	public void TryDecode_Valid(string address)
	{
		Assert.True(ZcashAddress.TryDecode(address, out _, out _, out ZcashAddress? addr));
		Assert.Equal(address, addr.ToString());
	}

	[Test, MethodDataSource(nameof(InvalidAddresses))]
	public void Parse_Invalid(string address)
	{
		Assert.Throws<InvalidAddressException>(() => ZcashAddress.Decode(address));
	}

	[Test, MethodDataSource(nameof(InvalidAddresses))]
	public void TryDecode_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryDecode(address, out _, out _, out _));
	}

	[Test]
	public void Parse_Null()
	{
		Assert.Throws<ArgumentNullException>(() => ZcashAddress.Decode(null!));
	}

	[Test]
	public void TryDecode_Null()
	{
		Assert.Throws<ArgumentNullException>(() => ZcashAddress.TryDecode(null!, out _, out _, out _));
	}

	[Test]
	[Arguments(ValidUnifiedAddressOrchard, typeof(OrchardAddress))]
	[Arguments(ValidUnifiedAddressOrchardSapling, typeof(UnifiedAddress))]
	[Arguments(ValidUnifiedAddressSapling, typeof(UnifiedAddress))]
	[Arguments(ValidSaplingAddress, typeof(SaplingAddress))]
	[Arguments(ValidSproutAddress, typeof(SproutAddress))]
	[Arguments(ValidTransparentP2PKHAddress, typeof(TransparentP2PKHAddress))]
	[Arguments(ValidTransparentP2SHAddress, typeof(TransparentP2SHAddress))]
	[Arguments(ValidTexAddress, typeof(TexAddress))]
	public void Decode_ReturnsAppropriateType(string address, Type expectedKind)
	{
		var addr = ZcashAddress.Decode(address);
		Assert.IsAssignableFrom(expectedKind, addr);
	}

	[Test]
	public void ImplicitlyCastableToString()
	{
		var addr = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		string str = addr.Address;
		Assert.Equal(ValidTransparentP2PKHAddress, str);
	}

	[Test]
	public void Equality()
	{
		var addr1a = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr1b = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr2 = ZcashAddress.Decode(ValidTransparentP2SHAddress);
		Assert.Equal(addr1a, addr1b);
		Assert.NotEqual(addr1a, addr2);
	}

	[Test]
	public void Equality_SameReceiversDifferentEncodings()
	{
		ZcashAddress saplingEncoded = ZcashAddress.Decode(ValidSaplingAddress);
		ZcashAddress unifiedEncoded = UnifiedAddress.Create(saplingEncoded);
		Assert.NotEqual(saplingEncoded, unifiedEncoded);
	}

	[Test]
	public void HashCodes()
	{
		var addr1a = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr1b = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr2 = ZcashAddress.Decode(ValidTransparentP2SHAddress);
		Assert.Equal(addr1a.GetHashCode(), addr1b.GetHashCode());
		Assert.NotEqual(addr1a.GetHashCode(), addr2.GetHashCode());
	}

	[Test]
	public void ImplicitCast()
	{
		string address = ZcashAddress.Decode(ValidTransparentP2SHAddress);
		Assert.Equal(ValidTransparentP2SHAddress, address);
	}

	[Test]
	public void ImplicitCast_Null()
	{
		string? address = (ZcashAddress?)null;
		Assert.Null(address);
	}

	[Test]
	[Arguments(ValidUnifiedAddressOrchard)]
	[Arguments(ValidUnifiedAddressOrchardSapling)]
	[Arguments(ValidUnifiedAddressSapling)]
	[Arguments(ValidSaplingAddress)]
	[Arguments(ValidSproutAddress)]
	[Arguments(ValidTransparentP2PKHAddress)]
	[Arguments(ValidTransparentP2SHAddress)]
	public void IsMatch_ExactMatch(string address)
	{
		Assert.Equal(MatchingReceiversFound, ZcashAddress.Decode(address).IsMatch(ZcashAddress.Decode(address)));
	}

	[Test]
	public void IsMatch_ExactMatch_DifferentEncodings()
	{
		ZcashAddress saplingEncoded = ZcashAddress.Decode(ValidSaplingAddress);
		ZcashAddress unifiedEncoded = UnifiedAddress.Create(saplingEncoded);
		Assert.Equal(MatchingReceiversFound, saplingEncoded.IsMatch(unifiedEncoded));
		Assert.Equal(MatchingReceiversFound, unifiedEncoded.IsMatch(saplingEncoded));
	}

	[Test]
	public void IsMatch_NoMatch()
	{
		Assert.Equal(
			NoMatchingReceiverTypes | UniqueReceiverTypesInReceivingAddress | UniqueReceiverTypesInTestAddress,
			ZcashAddress.Decode(ValidUnifiedAddressOrchard).IsMatch(ZcashAddress.Decode(ValidUnifiedAddressSapling)));
		Assert.Equal(MismatchingReceiversFound, ZcashAddress.Decode(ValidSaplingAddress2).IsMatch(ZcashAddress.Decode(ValidSaplingAddress)));
	}

	[Test]
	public void IsMatch_PartialMatch()
	{
		// Construct a case where one receiver matches and the other does not.
		UnifiedAddress receiver = (UnifiedAddress)ZcashAddress.Decode(ValidUnifiedAddressOrchardSapling);
		Bip39Mnemonic randomMnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);
		this.logger.WriteLine(randomMnemonic.SeedPhrase);
		ZcashAddress testAddress = UnifiedAddress.Create(
			receiver.Receivers.OfType<SaplingAddress>().Single(),
			Zip32HDWallet.Orchard.Create(randomMnemonic, receiver.Network).DefaultAddress);
		Assert.Equal(MatchingReceiversFound | MismatchingReceiversFound, receiver.IsMatch(testAddress));
	}
}
