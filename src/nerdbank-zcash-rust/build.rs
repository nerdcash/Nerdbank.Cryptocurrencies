fn main() {
    uniffi::generate_scaffolding("src/ffi.udl").unwrap();
}
