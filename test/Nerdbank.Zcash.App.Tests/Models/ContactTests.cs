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

	[Theory, PairwiseData]
	public void AllPropertiesSerialized(bool hasTransparentAddressAssigned)
	{
		Account account = new(new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits))));

		this.Model.Name = "Andrew";
		this.Model.ReceivingAddresses.Add(ZcashAddress.Decode("u1cyj7d9u44j6xrk8psq8vw52udd75yr0hslt8d2yhmn4trj4fm4pu0a9pukua4948tjqyw0ryzvea8qd6eup7kpj3u2ywer6ny47m8dl936f46chx0vlmqlx65pn87pwrklzzzwke6t4fzwp365s4vs4pyydcygjywtd25jqshsgy3fh5"));
		Contact.AssignedSendingAddresses assignment = this.Model.GetOrCreateSendingAddressAssignment(account);
		assignment.TransparentAddressIndex = hasTransparentAddressAssigned ? 3 : null;

		Contact deserialized = this.SerializeRoundtrip();

		Assert.Equal(this.Model.Name, deserialized.Name);
		Assert.Equal<ZcashAddress>(this.Model.ReceivingAddresses, deserialized.ReceivingAddresses);
		Contact.AssignedSendingAddresses deserializedAssignment = deserialized.AssignedAddresses.Single().Value;
		Assert.Equal(assignment.Diversifier, deserializedAssignment.Diversifier);
		Assert.Equal(assignment.TransparentAddressIndex, deserializedAssignment.TransparentAddressIndex);
	}
}
