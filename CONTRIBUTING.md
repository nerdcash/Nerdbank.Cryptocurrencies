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
* The [Rust toolset](https://rust-lang.org/tools/install/)
* Note that librustzcash is automatically brought in as a git submodule, make sure git submodule support is enabled with the following command: `git submodule update --init :/`.
* On Windows, some dependencies can be acquired by installing the "Build Tools" SKU in the "Visual Studio Installer" that you installed for Rust then click "More" → "Import Configuration" and import the ".vsconfig" file at the root of this project and install any components that come up.
* [NASM and CMake](https://aws.github.io/aws-lc-rs/requirements/index.html)

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

When adding or updating just one string in an existing file, the following AI prompt can be useful to provide the updated translations:

> Translate the following message to es, fr, ko, pt, ru, zh-Hans.
> The message is a localizable string in a software application that focuses on cryptocurrency.
> If the message includes macros in the form of `{somename}`, leave that macro untranslated and exactly the same as it was, including capitalization.

>> (your message here)

[pwsh]: https://learn.microsoft.com/powershell/scripting/install/installing-powershell

## Releases

Use `nbgv tag` to create a tag for a particular commit that you mean to release.
[Learn more about `nbgv` and its `tag` and `prepare-release` commands](https://dotnet.github.io/Nerdbank.GitVersioning/docs/nbgv-cli.html).

Push the tag.

### GitHub Actions

When your repo is hosted by GitHub and you are using GitHub Actions, you should create a GitHub Release using the standard GitHub UI.
Having previously used `nbgv tag` and pushing the tag will help you identify the precise commit and name to use for this release.

After publishing the release, the `.github/workflows/release.yml` workflow will be automatically triggered, which will:

1. Find the most recent `.github/workflows/build.yml` GitHub workflow run of the tagged release.
1. Upload the `deployables` artifact from that workflow run to your GitHub Release.
1. If you have `NUGET_API_KEY` defined as a secret variable for your repo or org, any nuget packages in the `deployables` artifact will be pushed to nuget.org.

### Azure Pipelines

When your repo builds with Azure Pipelines, use the `azure-pipelines/release.yml` pipeline.
Trigger the pipeline by adding the `auto-release` tag on a run of your main `azure-pipelines.yml` pipeline.

## Tutorial and API documentation

API and hand-written docs are found under the `docfx/` directory and are built by [docfx](https://dotnet.github.io/docfx/).

You can make changes and host the site locally to preview them by switching to that directory and running the `dotnet docfx --serve` command.
After making a change, you can rebuild the docs site while the localhost server is running by running `dotnet docfx` again from a separate terminal.

The `.github/workflows/docs.yml` GitHub Actions workflow publishes the content of these docs to github.io if the workflow itself and [GitHub Pages is enabled for your repository](https://docs.github.com/en/pages/quickstart).

## Updating dependencies

This repo uses Renovate to keep dependencies current.
Configuration is in the `.github/renovate.json` file.
[Learn more about configuring Renovate](https://docs.renovatebot.com/configuration-options/).

When changing the renovate.json file, follow [these validation steps](https://docs.renovatebot.com/config-validation/).

If Renovate is not creating pull requests when you expect it to, check that the [Renovate GitHub App](https://github.com/apps/renovate) is configured for your account or repo.

## Merging latest from Library.Template

### Maintaining your repo based on this template

The best way to keep your repo in sync with Library.Template's evolving features and best practices is to periodically merge the template into your repo:

```ps1
git fetch
git checkout origin/main
./tools/MergeFrom-Template.ps1
# resolve any conflicts, then commit the merge commit.
git push origin -u HEAD
```
