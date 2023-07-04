# Contributing

This project has adopted the [Microsoft Open Source Code of
Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct
FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com)
with any additional questions or comments.

## Best practices

* Use Windows PowerShell or [PowerShell Core][pwsh] (including on Linux/OSX) to run .ps1 scripts.
  Some scripts set environment variables to help you, but they are only retained if you use PowerShell as your shell.

## Prerequisites

All dependencies can be installed by running the `init.ps1` script at the root of the repository
using Windows PowerShell or [PowerShell Core][pwsh] (on any OS).
Some dependencies installed by `init.ps1` may only be discoverable from the same command line environment the init script was run from due to environment variables, so be sure to launch Visual Studio or build the repo from that same environment.
Alternatively, run `init.ps1 -InstallLocality Machine` (which may require elevation) in order to install dependencies at machine-wide locations so Visual Studio and builds work everywhere.

The prerequisites for building, testing, and deploying from this repository are:

* The [.NET SDK](https://get.dot.net/).
* The [Rust toolset](https://www.rust-lang.org/tools/install)

## Package restore

The easiest way to restore packages may be to run `init.ps1` which automatically authenticates
to the feeds that packages for this repo come from, if any.
`dotnet restore` or `nuget restore` also work but may require extra steps to authenticate to any applicable feeds.

## Building

This repository can be built on Windows, Linux, and OSX.

This repo contains rust and C#.
The rust code must be built first.
After running `init.ps1` as described above, run `src/nerdbank-zcash-rust/build_all.ps1` to build the rust dynamic library compatible with your OS before building the C# solution.

Building, testing, and packing the C# code can be done by using the standard dotnet CLI commands (e.g. `dotnet build`, `dotnet test`, `dotnet pack`, etc.).

[pwsh]: https://docs.microsoft.com/powershell/scripting/install/installing-powershell?view=powershell-6
