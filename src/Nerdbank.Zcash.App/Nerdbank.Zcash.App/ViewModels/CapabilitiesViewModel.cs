// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class CapabilitiesViewModel : ViewModelBase
{
	public string CapabilitiesHeading => CapabilitiesStrings.CapabilitiesHeading;

	public ImmutableList<Capability> Capabilities { get; } = [
		new(true, CapabilitiesStrings.InternalKeys_Title, CapabilitiesStrings.InternalKeys_Description),
		new(true, CapabilitiesStrings.Zip32_Title, CapabilitiesStrings.Zip32_Description),
		new(true, CapabilitiesStrings.MultipleAccounts_Title, CapabilitiesStrings.MultipleAccounts_Description),
		new(true, CapabilitiesStrings.Multispend_Title, CapabilitiesStrings.Multispend_Description),
		new(false, CapabilitiesStrings.PoolBalancing_Title, CapabilitiesStrings.PoolBalancing_Description),
		new(false, CapabilitiesStrings.MultiAccountSourcedTransactions_Title, CapabilitiesStrings.MultiAccountSourcedTransactions_Description),
		];

	public string PoolsHeading => CapabilitiesStrings.PoolsHeading;

	public ImmutableList<PoolSupport> Pools { get; } = [
		new(Pool.Transparent, true, true),
		new(Pool.Sprout, false, false),
		new(Pool.Sapling, true, true),
		new(Pool.Orchard, true, true),
		];

	public string PoolColumnHeader => string.Empty;

	public string CanSpendColumnHeader => CapabilitiesStrings.CanSpendColumnHeader;

	public string CanReceiveColumnHeader => CapabilitiesStrings.CanReceiveColumnHeader;

	public record PoolSupport(Pool Pool, bool CanSpend, bool CanReceive);

	public record Capability(bool IsEnabled, string Name, string? Description = null);
}
