## Functionality

- Windows app installer
- zcash: protocol handler to activate send view
  - Support multiple line items
- Setting to disable downloading too many blocks on metered networks.
- PERF: Optimize startup
  - AOT messagepack formatters
  - R2R assemblies with mibc data
  - Defer initializing LightClient until after the UI has loaded.
- Receiving screen: why doesn't an incoming transaction appear before its first confirmation?

## Views to build

- Messages/conversation view?
  Filter to just transactions with 0 value and non-empty text memos

## Views to enhance

- Transactions history
  - Add column to indicate unconfirmed or low confirmed transactions
  - Allow re-sending an expired transaction.
  - Add a To: field to show which receiving address was used (for in and outbound transactions).
  - Fix transaction amounts that are wrong in YWallet account.
- Sending
  - Support a scan of a zcash: QR code, possibly with multiple recipients
  - Enable transferring "everything" in the wallet (i.e. make working with fees easier when it's near the total).
  - Add private (mutable) memo field at the transaction level.
  - Offer protection from closing the app too soon and cancelling the broadcast.
- Receiving
  - Show a real indicator of a recent receipt of funds.
- Address book
  - Add QR code scanner for storing contact address.
- Backup
  - Offer shamir secret
