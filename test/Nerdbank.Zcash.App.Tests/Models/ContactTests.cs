// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class ContactTests : ModelTestBase<Contact>
{
	public ContactTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public Contact Contact { get; set; } = new();

	public override Contact Model => this.Contact;

	[Fact]
	public void FullyInitialized()
	{
		this.Model.Name = "Andrew";
		this.Model.ReceivingAddress = ZcashAddress.Decode("u1cyj7d9u44j6xrk8psq8vw52udd75yr0hslt8d2yhmn4trj4fm4pu0a9pukua4948tjqyw0ryzvea8qd6eup7kpj3u2ywer6ny47m8dl936f46chx0vlmqlx65pn87pwrklzzzwke6t4fzwp365s4vs4pyydcygjywtd25jqshsgy3fh5");
		Contact deserialized = this.SerializeRoundtrip();
		Assert.Equal(this.Model.Name, deserialized.Name);
		Assert.Equal(this.Model.ReceivingAddress, deserialized.ReceivingAddress);
	}
}
