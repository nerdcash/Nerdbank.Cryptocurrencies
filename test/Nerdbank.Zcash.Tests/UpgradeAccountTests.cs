// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Reflection;

public class UpgradeAccountTests : TestBase, IDisposable
{
	private readonly string testDir;
	private readonly LightWalletClient client;
	private readonly ZcashAccount spendingAccount;

	public UpgradeAccountTests()
	{
		this.testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(this.testDir);
		this.client = new LightWalletClient(
			new Uri("http://127.0.0.1:1"),
			ZcashNetwork.MainNet,
			Path.Join(this.testDir, "wallet.sqlite"));
		this.spendingAccount = new ZcashAccount(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));
	}

	public void Dispose()
	{
		this.client.Dispose();
		Directory.Delete(this.testDir, recursive: true);
	}

	[Test]
	public void UpgradeAccount_RejectsIncomingViewingKey()
	{
		ZcashAccount ivk = new(this.spendingAccount.IncomingViewing.UnifiedKey);
		NotSupportedException ex = Assert.Throws<NotSupportedException>(() => this.client.UpgradeAccount(ivk));
		Assert.Contains("incoming-viewing-key", ex.Message);
	}

	[Test]
	public void UpgradeAccount_RejectsFullViewingKeyWithoutSpendingKey()
	{
		ZcashAccount viewOnly = new(this.spendingAccount.FullViewing!.UnifiedKey);
		Assert.Null(viewOnly.Spending);
		NotSupportedException ex = Assert.Throws<NotSupportedException>(() => this.client.UpgradeAccount(viewOnly));
		Assert.Contains("no spending key", ex.Message);
	}

	[Test]
	public void UpgradeAccount_UnknownAccount_Throws()
	{
		Assert.Throws<InvalidOperationException>(() => this.client.UpgradeAccount(this.spendingAccount));
	}

	[Test]
	public void UpgradeAccount_PersistsSpendingCapability()
	{
		Remember(this.client, this.spendingAccount, Guid.NewGuid());
		LightWalletException ex = Assert.Throws<LightWalletException>(() => this.client.UpgradeAccount(this.spendingAccount));
		Assert.Equal(LightWalletException.ErrorCode.InvalidArgument, ex.Code);
	}

	[Test]
	public async Task AddAccountAsync_ExistingAccount_PersistsSpendingCapability()
	{
		Remember(this.client, this.spendingAccount, Guid.NewGuid());
		LightWalletException ex = await Assert.ThrowsAsync<LightWalletException>(
			() => this.client.AddAccountAsync(this.spendingAccount, CancellationToken.None));
		Assert.Equal(LightWalletException.ErrorCode.InvalidArgument, ex.Code);
	}

	/// <summary>
	/// Places <paramref name="account"/> in the client's in-memory map without importing it.
	/// </summary>
	private static void Remember(LightWalletClient client, ZcashAccount account, Guid id)
	{
		FieldInfo field = typeof(LightWalletClient).GetField("accountIds", BindingFlags.Instance | BindingFlags.NonPublic)!;
		ImmutableDictionary<ZcashAccount, Guid> accounts = (ImmutableDictionary<ZcashAccount, Guid>)field.GetValue(client)!;
		field.SetValue(client, accounts.Add(account, id));
	}
}
