// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class ContactManagerTests : ModelTestBase<ContactManager>
{
	public ContactManagerTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public override ContactManager Model { get; } = new();

	[Fact]
	public void NonEmptyContacts()
	{
		this.Model.Add(new Contact() { Name = "Alice" });
		ContactManager deserialized = this.SerializeRoundtrip();
		Assert.Equal("Alice", Assert.Single(deserialized.Contacts).Name);
	}
}
