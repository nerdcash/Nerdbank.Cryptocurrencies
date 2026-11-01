// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Data.Sqlite;

public class TransactionLineItemTests : TestBase
{
	[Test]
	public void Pool_Unspecified_StillInfersOrchard()
	{
		ZcashAccount account = new(new Zip32HDWallet(new byte[32], ZcashNetwork.TestNet), 0);
		OrchardAddress address = new(account.DefaultAddress.GetPoolReceiver<OrchardReceiver>()!.Value, account.Network);
		Transaction.LineItem item = new(address, 1m, Memo.NoMemo);

		Assert.Equal(Pool.Orchard, item.Pool);
		Assert.Equal(Pool.Ironwood, (item with { Pool = Pool.Ironwood }).Pool);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void GetDownloadedTransactions_Ironwood_PreservesPoolAndOrchardReceiver(bool multipleReceivers)
	{
		string testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(testDir);
		try
		{
			string dataFile = Path.Join(testDir, "wallet.sqlite");
			Uri server = new("http://localhost:1");
			using (LightWalletClient initializer = new(server, ZcashNetwork.TestNet, dataFile))
			{
				Assert.Empty(initializer.GetAccounts());
			}

			ZcashAccount account = new(new Zip32HDWallet(new byte[32], ZcashNetwork.TestNet), 0);
			OrchardReceiver receiver = account.DefaultAddress.GetPoolReceiver<OrchardReceiver>()!.Value;
			OrchardAddress expectedAddress = new(receiver, account.Network);
			Assert.NotNull(account.DefaultAddress.GetPoolReceiver<SaplingReceiver>());
			ZcashAddress savedAddress = multipleReceivers ? account.DefaultAddress : expectedAddress;
			using (SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = dataFile, Pooling = false }.ToString()))
			{
				connection.Open();
				using SqliteCommand command = connection.CreateCommand();
				command.CommandText = """
					INSERT INTO accounts (id, uuid, account_kind, ufvk, uivk, birthday_height, has_spend_key)
					VALUES (1, $uuid, 1, $ufvk, $uivk, 1, 0);
					INSERT INTO blocks (height, hash, time, sapling_tree)
					VALUES (100, zeroblob(32), 1000, X'000000');
					INSERT INTO transactions (id_tx, txid, block, mined_height, min_observed_height)
					VALUES (1, zeroblob(32), 100, 100, 100);
					INSERT INTO ironwood_received_notes
					(transaction_id, action_index, account_id, diversifier, value,
					 rho, rseed, is_change, memo, recipient_key_scope, note_version)
					VALUES (1, 0, 1, $diversifier, 20000, zeroblob(32), zeroblob(32), 0, X'68656c6c6f', 0, 2);
					INSERT INTO sent_notes
					(transaction_id, output_pool, output_index, from_account_id, to_account_id, to_address, value, memo)
					VALUES (1, 4, 0, 1, 1, $address, 20000, X'68656c6c6f');
					""";
				command.Parameters.AddWithValue("$uuid", Guid.NewGuid().ToByteArray());
				command.Parameters.AddWithValue("$ufvk", account.FullViewing!.UnifiedKey.ToString());
				command.Parameters.AddWithValue("$uivk", account.IncomingViewing.UnifiedKey.ToString());
				command.Parameters.AddWithValue("$diversifier", receiver.D.ToArray());
				command.Parameters.AddWithValue("$address", savedAddress.Address);
				command.ExecuteNonQuery();
			}

			using LightWalletClient client = new(server, account.Network, dataFile);
			ZcashAccount loadedAccount = Assert.Single(client.GetAccounts());
			Transaction transaction = Assert.Single(client.GetDownloadedTransactions(loadedAccount));
			Assert.Empty(transaction.Change);
			foreach (Transaction.LineItem line in new[] { Assert.Single(transaction.Incoming), Assert.Single(transaction.Outgoing) })
			{
				Assert.Equal(Pool.Ironwood, line.Pool);
				Assert.IsType<OrchardAddress>(line.ToAddress);
				Assert.Equal(expectedAddress, line.ToAddress);
				Assert.Equal(0.0002m, line.Amount);
			}
		}
		finally
		{
			Directory.Delete(testDir, recursive: true);
		}
	}
}
