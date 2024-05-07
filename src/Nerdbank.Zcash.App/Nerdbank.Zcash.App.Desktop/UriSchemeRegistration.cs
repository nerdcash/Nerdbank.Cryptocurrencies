// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft;
using Microsoft.Win32;

namespace Nerdbank.Zcash.App.Desktop;

/// <summary>
/// Handles registration of a URI scheme handler on Windows.
/// </summary>
internal record UriSchemeRegistration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UriSchemeRegistration"/> class.
	/// </summary>
	internal UriSchemeRegistration()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UriSchemeRegistration"/> class.
	/// </summary>
	/// <param name="scheme">The scheme of the URI that the app should handle.</param>
	/// <param name="pathToApp">The path to the application to launch. May be null to use the exe of the current process.</param>
	[SetsRequiredMembers]
	internal UriSchemeRegistration(string scheme, string? pathToApp = null)
	{
		this.Scheme = scheme;
		this.PathToApp = pathToApp ?? GetDefaultPathToApp();
	}

	/// <summary>
	/// Gets the scheme of the URI that the app should handle.
	/// </summary>
	public required string Scheme { get; init; }

	/// <summary>
	/// Gets the path to the app to launch.
	/// </summary>
	public required string PathToApp { get; init; }

	/// <summary>
	/// Gets the arguments that must be passed to the app on the command line when it is launched to handle a URI.
	/// </summary>
	/// <remarks>
	/// The URI itself is passed after any arguments specified in this property.
	/// </remarks>
	public ImmutableArray<string> ActivationArgs { get; init; } = ["--uri-handler", "--"];

	/// <summary>
	/// Registers a URI scheme handler.
	/// </summary>
	/// <param name="registration">The URI scheme information to register.</param>
	/// <remarks>
	/// If a prior protocol handler was registered, this method will overwrite it.
	/// </remarks>
	internal static void Register(UriSchemeRegistration registration)
	{
		if (OperatingSystem.IsWindows())
		{
			using RegistryKey? key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Classes\{registration.Scheme}");
			Verify.Operation(key is not null, "Creating zcash: URI scheme key failed.");
			key.SetValue(null, $"URL:{registration.Scheme}");
			key.SetValue("URL Protocol", string.Empty);

			using RegistryKey commandKey = key.CreateSubKey(@"shell\open\command");
			commandKey.SetValue(null, $@"""{registration.PathToApp}"" {FormatArgs(registration.ActivationArgs)} ""%1""");
		}
	}

	/// <summary>
	/// Removes registration of a particular URI scheme handler.
	/// </summary>
	/// <param name="registration">The URI scheme information to register.</param>
	internal static void Unregister(UriSchemeRegistration registration)
	{
		if (OperatingSystem.IsWindows())
		{
			// Before deleting the key, make sure it has _our_ data, so we don't delete another app's protocol
			// that had overridden ours.
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Classes\{registration.Scheme}");
			if (key is not null)
			{
				using RegistryKey? commandKey = key.OpenSubKey(@"shell\open\command");
				if (commandKey is not null)
				{
					string? command = commandKey.GetValue(null) as string;
					if (command is not null && command.Contains(registration.PathToApp, StringComparison.OrdinalIgnoreCase))
					{
						Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\Classes\zcash");
					}
				}
			}
		}
	}

	internal bool TryParseUriLaunch(string[] args, [NotNullWhen(true)] out Uri? uri)
	{
		if (args.Length == this.ActivationArgs.Length + 1 && args[..this.ActivationArgs.Length].SequenceEqual(this.ActivationArgs))
		{
			if (Uri.TryCreate(args[^1], UriKind.Absolute, out uri))
			{
				return true;
			}
		}

		uri = null;
		return false;
	}

	private static string GetDefaultPathToApp()
		=> Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Could not determine path to this app.");

	private static string FormatArgs(ImmutableArray<string> args)
	{
		StringBuilder argsBuilder = new();
		foreach (string arg in args)
		{
			if (argsBuilder.Length > 0)
			{
				argsBuilder.Append(' ');
			}

			if (arg.Contains(' '))
			{
				argsBuilder.Append('"');
				argsBuilder.Append(arg);
				argsBuilder.Append('"');
			}
			else
			{
				argsBuilder.Append(arg);
			}
		}

		return argsBuilder.ToString();
	}
}
