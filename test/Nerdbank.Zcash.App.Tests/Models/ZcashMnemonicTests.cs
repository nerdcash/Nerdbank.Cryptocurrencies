// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class ZcashMnemonicTests : ModelTestBase<ZcashMnemonic>
{
	public ZcashMnemonicTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public ZcashMnemonic ZcashMnemonic { get; private set; } = new();

	public override ZcashMnemonic Model => this.ZcashMnemonic;

	[Fact]
	public void Serialize()
	{
		this.ZcashMnemonic = new()
		{
			BirthdayHeight = 123456,
			IsBackedUp = true,
		};
		Assert.True(this.Model.IsDirty);

		ZcashMnemonic deserialized = this.SerializeRoundtrip();

		Assert.Equal(this.Model.IsBackedUp, deserialized.IsBackedUp);
		Assert.Equal(this.Model.BirthdayHeight, deserialized.BirthdayHeight);
		Assert.False(deserialized.IsDirty);
	}
}
