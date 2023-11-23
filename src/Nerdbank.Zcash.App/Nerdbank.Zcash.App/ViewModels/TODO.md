## Functionality

- Windows app installer
- zcash: protocol handler to activate send view
  - Support multiple line items

## Views to build

- Messages/conversation view?
  Filter to just transactions with 0 value and non-empty text memos

## Views to enhance

- Transactions history
  - Add column to indicate unconfirmed or low confirmed transactions
  - Allow re-sending an expired transaction.
  - Show amount in exchange rate that was current as of the transaction time.
  - Add Amount detail that shows the value transferred excluding the fee.
  - Add a To: field to show which receiving address was used (for in and outbound transactions).
- Sending
  - Support a scan of a zcash: QR code, possibly with multiple recipients
- Receiving
  - Show a real indicator of a recent receipt of funds.
- Address book
  - Add QR code scanner for storing contact address.
- Backup
  - Offer shamir secret
