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
* Note that Zingolib is automatically brought in as a git submodule, make sure git submodule support is enabled with the following command: `git submodule update --init :/`. If there are troubles compiling this then [compiling from source instructions are here](https://github.com/nerdcash/zingolib/tree/dev?tab=readme-ov-file#compiling-from-source). On Windows this can be done using [Perl Windows Download](https://strawberryperl.com/) and you can download [Protobuf](https://github.com/protocolbuffers/protobuf/releases) from here and put it in your path. Then to install the "Build Tools" on Windows launch "Visual Studio Installer" that you installed for Rust then click "More" → "Import Configuration" and import the ".vsconfig" file at the root of this project and install any components that come up.

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

## Translations

Human translations and corrections are welcome.
AI translations should be used for any new or modified English string to keep the translations up to date.
When using AI for translations, use the following prompt as a basis:

> Translate the `value` xml elements from English to **\{TARGET LANGUAGE\}**.
> Only translate the text in the `value` xml elements.
> When the value element text includes macros in the form of `{somename}`, leave that macro untranslated and exactly the same as it was, including capitalization.
> The text to translate comes from a software application specializing in cryptocurrencies.

When using Copilot within Visual Studio, select all the `<data>` elements in the English file and use the prompt above.
Then copy the translations into the language-specific file and revert the changes to the English file.

Web-based AI could also work by pasting the prompt and the `<data>` elements into the AI tool.

[pwsh]: https://docs.microsoft.com/powershell/scripting/install/installing-powershell?view=powershell-6
