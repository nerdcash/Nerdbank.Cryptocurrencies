// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class CapabilitiesViewModel : ViewModelBase
{
	public string CapabilitiesHeading => "Capabilities";

	public ImmutableList<Capability> Capabilities { get; } = [
		new(true, "Internal keys for shielding and change", "This protects the integrity of Incoming Viewing Keys (IVKs) so that they see no part of any spend transaction. After a wallet with this feature spends or shields funds, wallets that lack this feature will not see the full account balance."),
		new(true, "ZIP-32 HD wallets", "A human-readable \"seed phrase\" unlocks your accounts."),
		new(true, "Multiple accounts per seed phrase", "Use distinct accounts for spending and savings accounts while remembering or backing up just a single seed phrase."),
		new(true, "Multi-recipient spends", "Reduces network fees and mandatory wait times between multiple spends."),
		new(false, "Automatic shielded pool balancing", "This enhances privacy by reducing the likelihood that the value of a transaction (or a portion of it) will be publicly observable."),
		];

	public string PoolsHeading => "Supported Pools";

	public ImmutableList<PoolSupport> Pools { get; } = [
		new(Pool.Transparent, true, true),
		new(Pool.Sprout, false, false),
		new(Pool.Sapling, true, true),
		new(Pool.Orchard, true, true),
		];

	public string PoolColumnHeader => string.Empty;

	public string CanSpendColumnHeader => "Can Spend";

	public string CanReceiveColumnHeader => "Can Receive";

	public record PoolSupport(Pool Pool, bool CanSpend, bool CanReceive);

	public record Capability(bool IsEnabled, string Name, string? Description = null);
}
