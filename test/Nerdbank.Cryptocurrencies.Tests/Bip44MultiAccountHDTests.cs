// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using static Nerdbank.Cryptocurrencies.Bip32HDWallet;
using static Nerdbank.Cryptocurrencies.Bip44MultiAccountHD;

public class Bip44MultiAccountHDTests
{
	private readonly ITestOutputHelper logger;

	public Bip44MultiAccountHDTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void CreateKeyPath()
	{
		Assert.Equal("m/44'/133'/2'/1/4", Bip44MultiAccountHD.CreateKeyPath(0x80000085, 2, Change.ChangeAddressChain, 4).ToString());
		Assert.Equal("m/44'/133'/2'/1/4", Bip44MultiAccountHD.CreateKeyPath(133, 2 | HardenedBit, Change.ChangeAddressChain, 4).ToString());
	}

	/// <summary>
	/// Although it is not customary to harden the last two steps in the path, this test asserts that we allow it.
	/// </summary>
	[Fact]
	public void CreateKeyPath_HardenedLastParts()
	{
		Assert.Equal("m/44'/133'/2'/3'/4'", Bip44MultiAccountHD.CreateKeyPath(0x80000085, 2, (Change)(3 | HardenedBit), 4 | HardenedBit).ToString());
	}

	[Fact]
	public async Task DiscoverUsedAccountsAsync()
	{
		const int AddressGapLimit = 4;
		const string SearchExpected = """
			m/44'/133'/0'/0/0 N
			m/44'/133'/0'/0/1 N
			m/44'/133'/0'/0/2 Y

			m/44'/133'/1'/0/0 N
			m/44'/133'/1'/0/1 N
			m/44'/133'/1'/0/2 N
			m/44'/133'/1'/0/3 N
			""";
		await this.DiscoveryTestHelperAsync(SearchExpected, 3, d => Bip44MultiAccountHD.DiscoverUsedAccountsAsync(133, d, AddressGapLimit));
	}

	[Fact]
	public async Task DiscoverUsedAddressesAsync()
	{
		const int AddressGapLimit = 4;
		const string SearchExpected = """
			m/44'/133'/1'/0/0 N
			m/44'/133'/1'/0/1 N
			m/44'/133'/1'/0/2 Y
			m/44'/133'/1'/0/3 N
			m/44'/133'/1'/0/4 N
			m/44'/133'/1'/0/5 N
			m/44'/133'/1'/0/6 N
			m/44'/133'/1'/1/0 N
			m/44'/133'/1'/1/1 N
			m/44'/133'/1'/1/2 N
			m/44'/133'/1'/1/3 N
			""";
		KeyPath account = Bip44MultiAccountHD.CreateKeyPath(133, 1);
		await this.DiscoveryTestHelperAsync(SearchExpected, 5, d => Bip44MultiAccountHD.DiscoverUsedAddressesAsync(account, d, AddressGapLimit));
	}

	private async Task DiscoveryTestHelperAsync(string searchExpected, uint resultLevel, Func<Func<KeyPath, ValueTask<bool>>, IAsyncEnumerable<KeyPath>> discoveryFunction)
	{
		HashSet<KeyPath> reportFound = new();
		List<KeyPath> searchExpectedKeyPaths = searchExpected.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => !string.IsNullOrWhiteSpace(l)).Select(
			line =>
			{
				string[] pair = line.Trim().Split(' ');
				KeyPath keyPath = KeyPath.Parse(pair[0]);
				if (pair[1] == "Y")
				{
					reportFound.Add(keyPath);
				}

				return keyPath;
			}).ToList();
		ConcurrentBag<KeyPath> pathsSearched = new();
		ValueTask<bool> Discover(KeyPath keyPath)
		{
			pathsSearched.Add(keyPath);
			return new(reportFound.Contains(keyPath));
		}

		List<KeyPath> reportedFound = new();
		await foreach (KeyPath keyPath in discoveryFunction(Discover))
		{
			reportedFound.Add(keyPath);
		}

		this.logger.WriteLine("Paths searched:");
		foreach (KeyPath keyPath in pathsSearched.Order())
		{
			this.logger.WriteLine($"{keyPath} {(reportFound.Contains(keyPath) ? 'Y' : 'N')}");
		}

		Assert.Equal(searchExpectedKeyPaths, pathsSearched.Order());
		Assert.Equal(reportFound.Select(kp => kp.Truncate(resultLevel)).Order(), reportedFound.Order());

		this.logger.WriteLine(string.Empty);
		this.logger.WriteLine("Reported found:");
		foreach (KeyPath keyPath in reportedFound.Order())
		{
			this.logger.WriteLine($"{keyPath}");
		}
	}
}
