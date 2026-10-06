// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class OrchardAddressTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public OrchardAddressTests()
	{
		this.logger = TestOutputHelper.Instance;
	}

	[Test]
	public void Decode_TestNet()
	{
		Assert.Equal(ZcashNetwork.TestNet, ZcashAddress.Decode(ValidUnifiedAddressOrchardTestNet).Network);
	}

	[Test]
	public void HasShieldedReceiver() => Assert.True(ZcashAddress.Decode(ValidUnifiedAddressOrchard).HasShieldedReceiver);

	[Test]
	public void Ctor_Receiver_TestNet()
	{
		var receiver = new OrchardReceiver(new byte[88 / 8], new byte[256 / 8]);
		OrchardAddress addr = new(receiver, ZcashNetwork.TestNet);
		Assert.StartsWith("utest1", addr.ToString());
		this.logger.WriteLine(addr.Address);
	}
}
