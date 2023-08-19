// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Exposes functionality of a lightwallet client.
/// </summary>
public class LightWallet : IDisposable
{
	private readonly Uri serverUrl;
	private readonly ZcashNetwork network;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWallet"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="network">The Zcash network served by the server specified by <paramref name="serverUrl"/>.</param>
	public LightWallet(Uri serverUrl, ZcashNetwork network)
	{
		this.serverUrl = serverUrl;
		this.network = network;
	}

	/// <summary>
	/// Gets the height of the blockchain (independent of what may have been sync'd thus far.)
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The height of the blockchain.</returns>
	/// <exception cref="InvalidOperationException">Thrown if any error occurs.</exception>
	public ValueTask<ulong> GetLatestBlockHeightAsync(CancellationToken cancellationToken)
	{
		return new(Task.Run(
			delegate
			{
				long result = NativeMethods.lightwallet_get_block_height(this.serverUrl.AbsoluteUri);
				if (result < 0)
				{
					throw new InvalidOperationException();
				}

				return (ulong)result;
			},
			cancellationToken));
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		NativeMethods.lightwallet_deinitialize();
	}
}
