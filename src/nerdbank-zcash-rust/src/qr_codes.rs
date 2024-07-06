use encoding::all::UTF_16LE;
use encoding::DecoderTrap;
use encoding::Encoding;
use image::{self, DynamicImage, ImageResult};
use rqrr::PreparedImage;

const INVALID_UTF16_STRING: i32 = -1;
const IMAGE_ERROR: i32 = -2;
const QR_DECODE_ERROR: i32 = -3;
const QR_DECODE_NO_QR_CODE: i32 = 0;

#[no_mangle]
pub extern "C" fn decode_qr_code_from_file(
    file_path: *const u16,
    file_path_len: usize,
    decoded: *mut u16,
    decoded_length: usize,
) -> i32 {
    let file_path =
        unsafe { std::slice::from_raw_parts(file_path as *const u8, file_path_len * 2) };
    let file_path = match UTF_16LE.decode(file_path, DecoderTrap::Strict) {
        Ok(f) => f,
        Err(_) => return INVALID_UTF16_STRING,
    };

    decode_qr_code_interop_helper(image::open(file_path), decoded, decoded_length)
}

#[no_mangle]
pub extern "C" fn decode_qr_code_from_image(
    image_buffer: *const u8,
    image_buffer_len: usize,
    decoded: *mut u16,
    decoded_length: usize,
) -> i32 {
    let image_buffer = unsafe { std::slice::from_raw_parts(image_buffer, image_buffer_len) };

    decode_qr_code_interop_helper(
        image::load_from_memory(image_buffer),
        decoded,
        decoded_length,
    )
}

fn decode_qr_code_interop_helper(
    image: ImageResult<DynamicImage>,
    decoded: *mut u16,
    decoded_length: usize,
) -> i32 {
    match decode_qr_code_helper(image) {
        Ok(content) => {
            let decoded_slice = unsafe { std::slice::from_raw_parts_mut(decoded, decoded_length) };
            let content = content.encode_utf16();

            let mut content_len = 0;
            for (i, c) in content.enumerate() {
                if i < decoded_length {
                    decoded_slice[i] = c;
                }
                content_len += 1;
            }
            content_len
        }
        Err(e) => e,
    }
}

fn decode_qr_code_helper(image: ImageResult<DynamicImage>) -> Result<String, i32> {
    let image = image.map_err(|_e| IMAGE_ERROR)?;
    let image = image.to_luma8();
    let mut img = PreparedImage::prepare(image);
    let grids = img.detect_grids();
    match grids.len() {
        0 => Err(QR_DECODE_NO_QR_CODE),
        _ => {
            let (_, content) = grids[0].decode().map_err(|_e| QR_DECODE_ERROR)?;
            Ok(content)
        }
    }
}
