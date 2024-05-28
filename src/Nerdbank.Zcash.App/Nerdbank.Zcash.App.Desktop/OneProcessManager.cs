// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Nerdbank.Zcash.App;

public class OneProcessManager : IDisposable
{
	private static readonly string PipePrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\.\pipe" : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
	private Mutex? oneProcessMutex;
	private bool mutexOwned;

	public event EventHandler<SecondaryProcessEventArgs>? SecondaryProcessStarted;

	public SecondaryProcessEventArgs InstructionsForPrimaryProcess { get; init; } = new() { CommandLineArgs = Environment.GetCommandLineArgs()[1..] };

	public string? SessionUniquenessKey { get; init; } = Environment.ProcessPath;

	public bool TryClaimPrimaryProcess()
	{
		(string Mutex, string Pipe) names = GetSyncNamesFromUniquenessKey(this.SessionUniquenessKey ?? throw new InvalidOperationException($"Set {nameof(this.SessionUniquenessKey)} first."));

		this.oneProcessMutex ??= new Mutex(false, names.Mutex, out _);

		for (int i = 0; i < 10; i++)
		{
			try
			{
				this.mutexOwned |= this.oneProcessMutex.WaitOne(TimeSpan.Zero, true);
			}
			catch (AbandonedMutexException)
			{
				// We now own the mutex. There's nothing we need to do to clean up the previous owner.
				this.mutexOwned = true;
			}

			PipeOptions pipeOptions = PipeOptions.CurrentUserOnly;
			if (this.mutexOwned)
			{
				try
				{
					NamedPipeServerStream pipeServer = CreateServer();
					Task.Run(async delegate
					{
						while (true)
						{
							try
							{
								await pipeServer.WaitForConnectionAsync();
								if (IsRemoteProcessRunningSameProgramAsThisOne(pipeServer))
								{
									// Copy to another local because the original will be rewritten.
									PipeStream thisPipeServer = pipeServer;
									Task.Run(async delegate
									{
										try
										{
											SecondaryProcessEventArgs? args = await this.ReadSecondaryProcessMessageAsync(thisPipeServer);
											if (args is not null)
											{
												this.SecondaryProcessStarted?.Invoke(this, args);
											}
										}
										finally
										{
											await thisPipeServer.DisposeAsync();
										}
									}).Forget();
								}
							}
							catch (Exception ex)
							{
								Debug.Fail("Failed to handle secondary process IPC: " + ex.Message);
								await pipeServer.DisposeAsync();
							}

							pipeServer = CreateServer();
						}
					}).Forget();

					NamedPipeServerStream CreateServer() => new(names.Pipe, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, pipeOptions | PipeOptions.Asynchronous);
					return true;
				}
				catch (IOException)
				{
					// fall through to the secondary process path.
				}
			}
			else
			{
				try
				{
					using NamedPipeClientStream pipeClient = new(".", names.Pipe, PipeDirection.InOut, pipeOptions);
					pipeClient.Connect();
					if (!IsRemoteProcessRunningSameProgramAsThisOne(pipeClient))
					{
						pipeClient.Dispose();
						throw new InvalidOperationException("The primary process isn't running the same application!");
					}

					ActivatePrimaryProcess(pipeClient);
					this.CommunicateWithPrimaryProcess(pipeClient);

					return false;
				}
				catch (Exception ex) when (ex is IOException or InvalidOperationException)
				{
					// fall through to retry both paths.
				}
			}
		}

		throw new InvalidOperationException("Unable to establish primary or secondary process status.");
	}

	public void Dispose()
	{
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (this.oneProcessMutex is not null)
		{
			if (this.mutexOwned)
			{
				this.oneProcessMutex.ReleaseMutex();
			}

			this.oneProcessMutex.Dispose();
		}
	}

	/// <summary>
	/// Sends a message to the primary process (from a short-lived secondary process)
	/// to pass it whatever message is appropriate to construct its <see cref="SecondaryProcessEventArgs"/> object.
	/// </summary>
	/// <param name="pipe">The pipe over which to communicate with the parent process.</param>
	/// <remarks>
	/// Implementations are not required to dispose of the <paramref name="pipe"/> parameter.
	/// </remarks>
	protected virtual void CommunicateWithPrimaryProcess(PipeStream pipe)
	{
		if (this.InstructionsForPrimaryProcess.CommandLineArgs is not null)
		{
			using StreamWriter writer = new(pipe, Encoding.UTF8, 1024, leaveOpen: true);
			foreach (string arg in this.InstructionsForPrimaryProcess.CommandLineArgs)
			{
				writer.WriteLine(arg);
			}
		}
	}

	/// <summary>
	/// Reads a message from a secondary process to prepare to raise the <see cref="SecondaryProcessStarted"/> event.
	/// </summary>
	/// <param name="pipe">The pipe shared with the secondary process.</param>
	/// <returns>
	/// The event args passed to handlers of the <see cref="SecondaryProcessStarted"/> event;
	/// or <see langword="null" /> to suppress raising that event.
	/// </returns>
	/// <remarks>
	/// Implementations are not required to dispose of the <paramref name="pipe"/> parameter.
	/// </remarks>
	protected virtual async Task<SecondaryProcessEventArgs?> ReadSecondaryProcessMessageAsync(PipeStream pipe)
	{
		using StreamReader reader = new(pipe, Encoding.UTF8);
		List<string> args = new();
		string? line;
		while ((line = await reader.ReadLineAsync()) is not null)
		{
			args.Add(line);
		}

		SecondaryProcessEventArgs eventArgs = new()
		{
			CommandLineArgs = [.. args],
		};
		return eventArgs;
	}

	private static (string MutexName, string PipeName) GetSyncNamesFromUniquenessKey(string key)
	{
		// Unix requires pipe names to not exceed 108 characters.
		// The provided key may be much longer than that, so we'll hash it to a length
		// that is not expected to exceed the limit when appended to the pipe prefix.
		string hashedKey = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

		string mutexName = $"Local\\{hashedKey}";
		string pipeName = Path.Combine(PipePrefix, hashedKey);
		return (mutexName, pipeName);
	}

	private static bool IsRemoteProcessRunningSameProgramAsThisOne(NamedPipeClientStream pipe)
	{
		if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
		{
			return PInvoke.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId) && IsProcessRunningSameProgramAsThisOne((int)processId);
		}
		else
		{
			return true;
		}
	}

	private static bool IsRemoteProcessRunningSameProgramAsThisOne(NamedPipeServerStream pipe)
	{
		if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
		{
			return PInvoke.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint processId) && IsProcessRunningSameProgramAsThisOne((int)processId);
		}
		else
		{
			return true;
		}
	}

	private static bool IsProcessRunningSameProgramAsThisOne(int processId)
	{
		using Process otherProcess = Process.GetProcessById(processId);
		return string.Equals(otherProcess.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
	}

	private static void ActivatePrimaryProcess(NamedPipeClientStream clientPipe)
	{
		if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
		{
			if (PInvoke.GetNamedPipeServerProcessId(clientPipe.SafePipeHandle, out uint primaryProcessPid))
			{
				using Process p = Process.GetProcessById((int)primaryProcessPid);
				HWND hWnd = (HWND)p.MainWindowHandle;
				if (!PInvoke.SetForegroundWindow(hWnd))
				{
					Debug.Fail("Failed to activate the primary process.");
					return;
				}

				WINDOWPLACEMENT windowPlacement = default;
				if (!PInvoke.GetWindowPlacement(hWnd, ref windowPlacement))
				{
					Debug.Fail("Failed to get the window's current placement.");
					return;
				}

				if (windowPlacement.showCmd == SHOW_WINDOW_CMD.SW_SHOWMINIMIZED)
				{
					// SW_RESTORE will un-minimize the window to its 'normal/restored' or maximized state -- whatever it was in previously.
					if (!PInvoke.ShowWindowAsync(hWnd, SHOW_WINDOW_CMD.SW_RESTORE))
					{
						Debug.Fail("Failed to un-minimize the primary process window.");
						return;
					}
				}
			}
		}
	}

	public class SecondaryProcessEventArgs : EventArgs
	{
		public string[]? CommandLineArgs { get; init; }
	}
}
