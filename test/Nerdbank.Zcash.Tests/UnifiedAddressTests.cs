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
		Assert.Equal(0, addr.Revision);
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
		Assert.Equal(0, addr.Revision);
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
		Assert.Equal(0, addr.Revision);
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

	/// <summary>
	/// Asserts that a UA with only a transparent receiver and no metadata fails
	/// to be created due to not satisfying the minimum length requirement for F4Jumble.
	/// </summary>
	[Fact]
	public void TryCreate_TransparentOnly_NoMetadata()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));
		DiversifierIndex index = default;

		// Although this is a Try method, we expect an exception to be thrown because
		// the Try only returns false when the index is too high to find a valid diversifier.
		ArgumentException ex = Assert.Throws<ArgumentException>(() => UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Transparent!], out UnifiedAddress? _));
		this.logger.WriteLine(ex.Message);
	}

	[Theory, PairwiseData]
	public void TryCreate_TryDecode_TransparentOnly(ZcashNetwork network)
	{
		string expectedHRP = network switch
		{
			ZcashNetwork.MainNet => "ur",
			ZcashNetwork.TestNet => "urtest",
			_ => throw new ArgumentOutOfRangeException(nameof(network)),
		};

		// We have to include expiration block metadata to push the length of the encoded data
		// to the required 40 bytes.
		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationHeight = 100,
		};

		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, network));

		DiversifierIndex index = default;
		Assert.True(UnifiedAddress.TryCreate(ref index, [account.IncomingViewing.Transparent!], metadata, out UnifiedAddress? address));
		this.logger.WriteLine(address);
		Assert.Equal(1, address.Revision);
		Assert.NotNull(address.GetPoolReceiver<TransparentP2PKHReceiver>());
		Assert.StartsWith($"{expectedHRP}1", address.Address); // Revision 1+ is required for UAs with only a transparent receiver.

		Assert.True(ZcashAddress.TryDecode(address.Address, out _, out _, out ZcashAddress? address2));
		Assert.Equal(address, address2);
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

	[Theory]
	[InlineData(ValidSaplingAddress)]
	[InlineData(ValidUnifiedAddressOrchard)]
	[InlineData(ValidTransparentP2PKHAddress)]
	public void MetadataEncoding(string address)
	{
		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};

		UnifiedAddress ua = UnifiedAddress.Create([ZcashAddress.Decode(address)], metadata);
		this.logger.WriteLine(ua);
		Assert.Equal(metadata, ua.Metadata);
		Assert.Equal(metadata, ((UnifiedAddress)ZcashAddress.Decode(ua.Address)).Metadata);
	}

	[Theory]
	[InlineData(ValidSaplingAddress)]
	[InlineData(ValidUnifiedAddressOrchard)]
	public void WithMetadata(string address)
	{
		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};

		UnifiedAddress ua = UnifiedAddress.Create(ZcashAddress.Decode(address))
			.WithMetadata(metadata);

		Assert.Equal(metadata, ua.Metadata);
		Assert.Equal(metadata, ((UnifiedAddress)ZcashAddress.Decode(ua.Address)).Metadata);
	}

	[Fact]
	public void WithMetadata_Transparent()
	{
		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};
		UnifiedEncodingMetadata metadata2 = metadata with
		{
			ExpirationHeight = 1_000_001,
		};

		// A UA with only a transparent receiver is too short for a UA.
		// Metadata is required to make the encoding longer so that it is allowed.
		UnifiedAddress ua = UnifiedAddress.Create([ZcashAddress.Decode(ValidTransparentP2PKHAddress)], metadata)
			.WithMetadata(metadata2);

		Assert.Equal(metadata2, ua.Metadata);
		Assert.Equal(metadata2, ((UnifiedAddress)ZcashAddress.Decode(ua.Address)).Metadata);
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
