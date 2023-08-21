// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using uniffi.LightWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// Exposes functionality of a lightwallet client.
/// </summary>
public class LightWallet : IDisposableObservable
{
	private readonly Uri serverUrl;
	private readonly ZcashNetwork network;
	private readonly LightWalletSafeHandle handle;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWallet"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="network">The Zcash network served by the server specified by <paramref name="serverUrl"/>.</param>
	/// <param name="walletPath"><inheritdoc cref="NativeMethods.lightwallet_initialize(string, ZcashNetwork, string, string, string, bool)" path="/param[@name='data_dir']"/></param>
	/// <param name="walletName"><inheritdoc cref="NativeMethods.lightwallet_initialize(string, ZcashNetwork, string, string, string, bool)" path="/param[@name='wallet_name']"/></param>
	/// <param name="logName"><inheritdoc cref="NativeMethods.lightwallet_initialize(string, ZcashNetwork, string, string, string, bool)" path="/param[@name='log_name']"/></param>
	/// <param name="watchMemPool"><inheritdoc cref="NativeMethods.lightwallet_initialize(string, ZcashNetwork, string, string, string, bool)" path="/param[@name='monitor_mempool']"/></param>
	public LightWallet(Uri serverUrl, ZcashNetwork network, string walletPath, string walletName, string logName, bool watchMemPool)
	{
		Requires.NotNull(serverUrl);

		this.serverUrl = serverUrl;
		this.network = network;
		this.handle = new LightWalletSafeHandle(
			NativeMethods.lightwallet_initialize(
				serverUrl.AbsoluteUri,
				network,
				walletPath,
				walletName,
				logName,
				watchMemPool),
			ownsHandle: true);
	}

	/// <inheritdoc/>
	bool IDisposableObservable.IsDisposed => this.disposed;

	/// <summary>
	/// Gets the height of the blockchain (independent of what may have been sync'd thus far.)
	/// </summary>
	/// <param name="lightWalletServerUrl">The URL of the lightwallet server to query.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The height of the blockchain.</returns>
	/// <exception cref="InvalidOperationException">Thrown if any error occurs.</exception>
	public static ValueTask<ulong> GetLatestBlockHeightAsync(Uri lightWalletServerUrl, CancellationToken cancellationToken)
	{
		return new(Task.Run(
			delegate
			{
				long result = LightWalletMethods.LightwalletGetBlockHeight(lightWalletServerUrl.AbsoluteUri);
				if (result < 0)
				{
					throw new InvalidOperationException();
				}

				return (ulong)result;
			},
			cancellationToken));
	}

	/// <inheritdoc cref="GetLatestBlockHeightAsync(Uri, CancellationToken)"/>
	public ValueTask<ulong> GetLatestBlockHeightAsync(CancellationToken cancellationToken) => GetLatestBlockHeightAsync(this.serverUrl, cancellationToken);

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposed = true;
		this.handle.Dispose();
	}

	private class LightWalletSafeHandle : SafeHandle
	{
		public LightWalletSafeHandle(nint invalidHandleValue, bool ownsHandle)
			: base(invalidHandleValue, ownsHandle)
		{
		}

		public override bool IsInvalid => this.handle <= 0;

		protected override bool ReleaseHandle() => NativeMethods.lightwallet_deinitialize(this.handle) == 0;
	}
}
