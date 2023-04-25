// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class UnifiedAddressTests : TestBase
{
	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { "u1" },
		new object?[] { "u1oecuh" },
	};

	[Fact]
	public void Receivers_UnifiedMultiple()
	{
		UnifiedAddress addr = Assert.IsAssignableFrom<UnifiedAddress>(ZcashAddress.Parse(ValidUnifiedAddressOrchardSapling));
		Assert.Equal(
			new[]
			{
				ZcashAddress.Parse(ValidUnifiedAddressOrchard),
				ZcashAddress.Parse(ValidSaplingAddress),
			},
			addr.Receivers);

		addr = Assert.IsAssignableFrom<UnifiedAddress>(ZcashAddress.Parse(ValidUnifiedAddressOrchardSaplingTransparentP2PKH));
		Assert.Equal(
			new[]
			{
				ZcashAddress.Parse(ValidUnifiedAddressOrchard),
				ZcashAddress.Parse(ValidSaplingAddress),
				ZcashAddress.Parse(ValidTransparentP2PKHAddress),
			},
			addr.Receivers);

		addr = Assert.IsAssignableFrom<UnifiedAddress>(ZcashAddress.Parse(ValidUnifiedAddressOrchardSaplingTransparentP2SH));
		Assert.Equal(
			new[]
			{
				ZcashAddress.Parse(ValidUnifiedAddressOrchard),
				ZcashAddress.Parse(ValidSaplingAddress),
				ZcashAddress.Parse(ValidTransparentP2SHAddress),
			},
			addr.Receivers);
	}

	[Fact]
	public void GetPoolReceiver()
	{
		UnifiedAddress ua = (UnifiedAddress)ZcashAddress.Parse(ValidUnifiedAddressOrchardSaplingTransparentP2PKH);
		OrchardReceiver orchard = ua.GetPoolReceiver<OrchardReceiver>() ?? throw new Exception("Missing Orchard receiver");
		SaplingReceiver sapling = ua.GetPoolReceiver<SaplingReceiver>() ?? throw new Exception("Missing Sapling receiver");
		TransparentP2PKHReceiver p2pkh = ua.GetPoolReceiver<TransparentP2PKHReceiver>() ?? throw new Exception("Missing P2PKH receiver");
		Assert.Null(ua.GetPoolReceiver<TransparentP2SHReceiver>());
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
			ZcashAddress.Parse(ValidSaplingAddress),
			ZcashAddress.Parse(ValidSproutAddress),
		}));
	}

	[Fact]
	public void Create_RejectsP2SHandP2PKHTogether()
	{
		// Per the spec, only one transparent address (of either type) is allowed in a UA.
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Parse(ValidSaplingAddress),
			ZcashAddress.Parse(ValidTransparentP2SHAddress),
			ZcashAddress.Parse(ValidTransparentP2PKHAddress),
		}));
	}

	[Fact]
	public void Create_RejectsTwoSaplings()
	{
		Assert.Throws<ArgumentException>(() => UnifiedAddress.Create(new[]
		{
			ZcashAddress.Parse(ValidSaplingAddress),
			ZcashAddress.Parse(ValidSaplingAddress2),
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
			ZcashAddress.Parse(ValidUnifiedAddressOrchardSapling),
			ZcashAddress.Parse(ValidUnifiedAddressOrchardSaplingTransparentP2PKH),
		}));
	}

	[Fact]
	public void Create()
	{
		UnifiedAddress addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Parse(ValidUnifiedAddressOrchard),
			ZcashAddress.Parse(ValidSaplingAddress),
			ZcashAddress.Parse(ValidTransparentP2PKHAddress),
		});
		Assert.Equal(ValidUnifiedAddressOrchardSaplingTransparentP2PKH, addr.ToString());

		addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Parse(ValidUnifiedAddressOrchard),
			ZcashAddress.Parse(ValidSaplingAddress),
			ZcashAddress.Parse(ValidTransparentP2SHAddress),
		});
		Assert.Equal(ValidUnifiedAddressOrchardSaplingTransparentP2SH, addr.ToString());
	}

	[Fact]
	public void Create_OrchardOnly()
	{
		UnifiedAddress addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Parse(ValidUnifiedAddressOrchard),
		});
		Assert.Equal(ValidUnifiedAddressOrchard, addr.ToString());
	}

	[Fact]
	public void Create_SaplingOnly()
	{
		UnifiedAddress addr = UnifiedAddress.Create(new[]
		{
			ZcashAddress.Parse(ValidSaplingAddress),
		});
		Assert.Equal(ValidUnifiedAddressSapling, addr.ToString());
	}

	[Fact]
	public void Network()
	{
		Assert.Equal(ZcashNetwork.MainNet, ZcashAddress.Parse(ValidUnifiedAddressSapling).Network);
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryParse_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryParse(address, out _));
	}
}
