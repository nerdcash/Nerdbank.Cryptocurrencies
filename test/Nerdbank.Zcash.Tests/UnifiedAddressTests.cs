// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class UnifiedAddressTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public UnifiedAddressTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { "u1" },
		new object?[] { "u1oecuh" },
	};

	[Fact]
	public void Receivers_UnifiedMultiple()
	{
		UnifiedAddress addr = Assert.IsAssignableFrom<UnifiedAddress>(ZcashAddress.Decode(ValidUnifiedAddressOrchardSapling));
		Assert.Equal(
			new[]
			{
				ZcashAddress.Decode(ValidUnifiedAddressOrchard),
				ZcashAddress.Decode(ValidSaplingAddress),
			},
			addr.Receivers);

		addr = Assert.IsAssignableFrom<UnifiedAddress>(ZcashAddress.Decode(ValidUnifiedAddressOrchardSaplingTransparentP2PKH));
		Assert.Equal(
			new[]
			{
				ZcashAddress.Decode(ValidUnifiedAddressOrchard),
				ZcashAddress.Decode(ValidSaplingAddress),
				ZcashAddress.Decode(ValidTransparentP2PKHAddress),
			},
			addr.Receivers);

		addr = Assert.IsAssignableFrom<UnifiedAddress>(ZcashAddress.Decode(ValidUnifiedAddressOrchardSaplingTransparentP2SH));
		Assert.Equal(
			new[]
			{
				ZcashAddress.Decode(ValidUnifiedAddressOrchard),
				ZcashAddress.Decode(ValidSaplingAddress),
				ZcashAddress.Decode(ValidTransparentP2SHAddress),
			},
			addr.Receivers);
	}

	[Fact]
	public void GetPoolReceiver()
	{
		var ua = (UnifiedAddress)ZcashAddress.Decode(ValidUnifiedAddressOrchardSaplingTransparentP2PKH);
		OrchardReceiver orchard = ua.GetPoolReceiver<OrchardReceiver>() ?? throw new Exception("Missing Orchard receiver");
		SaplingReceiver sapling = ua.GetPoolReceiver<SaplingReceiver>() ?? throw new Exception("Missing Sapling receiver");
		TransparentP2PKHReceiver p2pkh = ua.GetPoolReceiver<TransparentP2PKHReceiver>() ?? throw new Exception("Missing P2PKH receiver");
		Assert.Null(ua.GetPoolReceiver<TransparentP2SHReceiver>());
	}

	[Fact]
	public void HasShieldedReceiver()
	{
		Assert.True(ZcashAddress.Decode(ValidUnifiedAddressOrchardSaplingTransparentP2PKH).HasShieldedReceiver);
		Assert.True(ZcashAddress.Decode(ValidUnifiedAddressSapling).HasShieldedReceiver);
	}

	[Fact]
	public void Create_RejectsEmptyInputs()
	{
		Assert.Throws<ArgumentNullException>(() => UnifiedAddress.Create(null!));
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(Array.Empty<ZcashAddress>()));
	}

	[Fact]
	public void Create_RejectsSproutAddresses()
	{
		// Per the spec, sprout addresses are not allowed.
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidSaplingAddress),
			ZcashAddress.Decode(ValidSproutAddress),
		}));
	}

	[Fact]
	public void Create_RejectsP2SHandP2PKHTogether()
	{
		// Per the spec, only one transparent address (of either type) is allowed in a UA.
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidSaplingAddress),
			ZcashAddress.Decode(ValidTransparentP2SHAddress),
			ZcashAddress.Decode(ValidTransparentP2PKHAddress),
		}));
	}

	[Fact]
	public void Create_RejectsTwoSaplings()
	{
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidSaplingAddress),
			ZcashAddress.Decode(ValidSaplingAddress2),
		}));
	}

	/// <summary>
	/// Verifies an exception when one of the receivers is itself a unified address that contains multiple receivers.
	/// </summary>
	[Fact]
	public void Create_WithCompoundUnifiedReceiver()
	{
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidUnifiedAddressOrchardSapling),
			ZcashAddress.Decode(ValidUnifiedAddressOrchardSaplingTransparentP2PKH),
		}));
	}

	[Fact]
	public void Create()
	{
		var addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidUnifiedAddressOrchard),
			ZcashAddress.Decode(ValidSaplingAddress),
			ZcashAddress.Decode(ValidTransparentP2PKHAddress),
		});
		Assert.Equal(ValidUnifiedAddressOrchardSaplingTransparentP2PKH, addr.ToString());

		addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidUnifiedAddressOrchard),
			ZcashAddress.Decode(ValidSaplingAddress),
			ZcashAddress.Decode(ValidTransparentP2SHAddress),
		});
		Assert.Equal(ValidUnifiedAddressOrchardSaplingTransparentP2SH, addr.ToString());
	}

	[Fact]
	public void Create_OrchardSapling_TestNet()
	{
		var addr = UnifiedAddress.Create(
			new[]
			{
				ZcashAddress.Decode(ValidUnifiedAddressOrchardTestNet),
				ZcashAddress.Decode(ValidSaplingAddressTestNet),
			});
		this.logger.WriteLine(addr.Address);
		Assert.Equal(ZcashNetwork.TestNet, addr.Network);
		Assert.StartsWith("utest1", addr.Address);
		Assert.Equal(ValidUnifiedAddressOrchardSaplingTestNet, addr.ToString());
	}

	[Fact]
	public void Create_OrchardOnly_TestNet()
	{
		var addr = UnifiedAddress.Create(
			new[]
			{
				ZcashAddress.Decode(ValidUnifiedAddressOrchardTestNet),
			});
		this.logger.WriteLine(addr.Address);
		Assert.Equal(ZcashNetwork.TestNet, addr.Network);
		Assert.StartsWith("utest1", addr.Address);
		Assert.Equal(ValidUnifiedAddressOrchardTestNet, addr.ToString());
	}

	[Fact]
	public void Create_RejectsMixedNetworks()
	{
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidSaplingAddressTestNet),
			ZcashAddress.Decode(ValidUnifiedAddressOrchard),
		}));
	}

	[Fact]
	public void Create_OrchardOnly()
	{
		var addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidUnifiedAddressOrchard),
		});
		this.logger.WriteLine(addr.Address);
		Assert.Equal(ValidUnifiedAddressOrchard, addr.ToString());
	}

	[Fact]
	public void Create_SaplingOnly()
	{
		var addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Decode(ValidSaplingAddress),
		});
		this.logger.WriteLine(addr.Address);
		Assert.Equal(ZcashNetwork.MainNet, addr.Network);
		Assert.Equal(ValidUnifiedAddressSapling, addr.ToString());
	}

	[Fact]
	public void Create_SaplingOnly_TestNet()
	{
		var addr = UnifiedAddress.Create(
			new[]
			{
				ZcashAddress.Decode(ValidSaplingAddressTestNet),
			});
		this.logger.WriteLine(addr.Address);
		Assert.Equal(ZcashNetwork.TestNet, addr.Network);
		Assert.StartsWith("utest1", addr.Address);
		Assert.Equal(ValidUnifiedAddressSaplingTestNet, addr.ToString());
	}

	[Fact]
	public void TryCreate_TransparentOnly()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex index = default;
		Assert.Throws<ArgumentException>(() => UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Transparent!], out UnifiedAddress? address));
	}

	[Fact]
	public void TryCreate_SaplingOnly()
	{
		// The particular mnemonic used here has a sapling key that doesn't produce a valid diversifier until index 3.
		// But we're excluding sapling from this UA, so we expect a 0 index UA.
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex index = default;
		Assert.True(UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Sapling!], out UnifiedAddress? address));
		Assert.Equal(3, index.ToBigInteger());

		AssertAddressIndex(account, index, address);
	}

	[Fact]
	public void TryCreate_WithoutNonZeroSaplingComponent()
	{
		// The particular mnemonic used here has a sapling key that doesn't produce a valid diversifier until index 3.
		// But we're excluding sapling from this UA, so we expect a 0 index UA.
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex index = default;
		Assert.True(UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Orchard!, account.IncomingViewing.Transparent!], out UnifiedAddress? address));
		Assert.Equal(default, index);

		AssertAddressIndex(account, index, address);
	}

	[Fact]
	public void TryCreate_MatchingIndexedReceivers()
	{
		// The particular mnemonic used here has a sapling key that doesn't produce a valid diversifier until index 3.
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex index = default;
		Assert.True(UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Orchard!, account.IncomingViewing.Sapling!, account.IncomingViewing.Transparent!], out UnifiedAddress? address));

		AssertAddressIndex(account, index, address);
	}

	[Fact]
	public void TryCreate_TooHighForTransparent()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex index = new(ulong.MaxValue);
		Assert.False(UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Orchard!, account.IncomingViewing.Sapling!, account.IncomingViewing.Transparent!], out UnifiedAddress? address));
		Assert.Null(address);
	}

	[Fact]
	public void Network_AfterParse()
	{
		Assert.Equal(ZcashNetwork.MainNet, ZcashAddress.Decode(ValidUnifiedAddressSapling).Network);
		Assert.Equal(ZcashNetwork.TestNet, ZcashAddress.Decode(ValidUnifiedAddressSaplingTestNet).Network);
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryDecode_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryDecode(address, out _, out _, out _));
	}

	internal static void AssertAddressIndex(ZcashAccount account, DiversifierIndex expectedIndex, UnifiedAddress address)
	{
		if (address.GetPoolReceiver<OrchardReceiver>() is OrchardReceiver orchard)
		{
			Assert.True(account.IncomingViewing.Orchard!.TryGetDiversifierIndex(orchard, out DiversifierIndex? actualOrchardIndex));
			Assert.Equal(expectedIndex, actualOrchardIndex);
		}

		if (address.GetPoolReceiver<SaplingReceiver>() is SaplingReceiver sapling)
		{
			Assert.True(account.IncomingViewing.Sapling!.TryGetDiversifierIndex(sapling, out DiversifierIndex? actualSaplingIndex));
			Assert.Equal(expectedIndex, actualSaplingIndex);
		}

		if (address.GetPoolReceiver<TransparentP2PKHReceiver>() is TransparentP2PKHReceiver actualTransparentReceiver)
		{
			TransparentP2PKHReceiver expectedTransparentReceiver = account.IncomingViewing.Transparent!.GetReceivingKey(checked((uint)expectedIndex.ToBigInteger())).DefaultAddress.GetPoolReceiver<TransparentP2PKHReceiver>()!.Value;
			Assert.Equal(expectedTransparentReceiver, actualTransparentReceiver);
		}
	}
}
