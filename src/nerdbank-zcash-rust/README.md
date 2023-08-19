We use a sad hack here, because the `ring` crate is either broken on win-arm64, or broken on win-x64.
So we have *two* Cargo.toml files, and swap in the necessary one based on what we're building.
