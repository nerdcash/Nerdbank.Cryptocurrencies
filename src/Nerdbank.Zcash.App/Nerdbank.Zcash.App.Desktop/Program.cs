// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Nerdbank.Zcash.App.Desktop;

internal class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args) => BuildAvaloniaApp()
		.StartWithClassicDesktopLifetime(args);

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
	{
		AppBuilder builder = AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.WithInterFont()
				.LogToTrace()
				.UseReactiveUI();

		// Workaround for transparent Window on win-arm64 (https://github.com/AvaloniaUI/Avalonia/issues/10405)
		if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
		{
			builder = builder.UseWin32()
				.With(new Win32PlatformOptions
				{
					RenderingMode = new[] { Win32RenderingMode.Software },
				});
		}

		return builder;
	}
}
