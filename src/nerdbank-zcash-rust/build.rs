fn main() -> Result<(), Box<dyn std::error::Error>> {
    uniffi::generate_scaffolding("src/ffi.udl").unwrap();
    Ok(())
}
