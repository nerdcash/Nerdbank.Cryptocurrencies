// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

public class WindowsHtmlClipboardEncoderTests(ITestOutputHelper logger)
{
	[Fact]
	public void HtmlClipboardFormatName() => Assert.Equal("HTML Format", WindowsHtmlClipboardEncoder.HtmlClipboardFormatName);

	[Fact]
	public void Encode()
	{
		// The 'expected' comes from HTML copied from OneNote.
		string expected = "Version:1.0\r\nStartHTML:0000000105\r\nEndHTML:0000000694\r\nStartFragment:0000000588\r\nEndFragment:0000000648\r\n\r\n<html xmlns:o=\"urn:schemas-microsoft-com:office:office\"\r\nxmlns:dt=\"uuid:C2F41010-65B3-11d1-A29F-00AA00C14882\"\r\nxmlns=\"http://www.w3.org/TR/REC-html40\">\r\n\r\n<head>\r\n<meta http-equiv=Content-Type content=\"text/html; charset=utf-8\">\r\n<meta name=ProgId content=OneNote.File>\r\n<meta name=Generator content=\"Microsoft OneNote 15\">\r\n</head>\r\n\r\n<body lang=en-US style='font-family:Calibri;font-size:11.0pt'>\r\n\r\n<p style='margin:0in;font-family:Calibri;font-size:11.0pt'><!--StartFragment--><span\r\nstyle='mso-spacerun:yes'> </span>two on opposing sid<!--EndFragment--></p>\r\n\r\n</body>\r\n\r\n</html>\r\n";
		byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);

		byte[] actualBytes = WindowsHtmlClipboardEncoder.Encode(
			"\r\n<html xmlns:o=\"urn:schemas-microsoft-com:office:office\"\r\nxmlns:dt=\"uuid:C2F41010-65B3-11d1-A29F-00AA00C14882\"\r\nxmlns=\"http://www.w3.org/TR/REC-html40\">\r\n\r\n<head>\r\n<meta http-equiv=Content-Type content=\"text/html; charset=utf-8\">\r\n<meta name=ProgId content=OneNote.File>\r\n<meta name=Generator content=\"Microsoft OneNote 15\">\r\n</head>\r\n\r\n<body lang=en-US style='font-family:Calibri;font-size:11.0pt'>\r\n\r\n<p style='margin:0in;font-family:Calibri;font-size:11.0pt'>",
			"<span\r\nstyle='mso-spacerun:yes'> </span>two on opposing sid",
			"</p>\r\n\r\n</body>\r\n\r\n</html>\r\n");
		string actual = Encoding.UTF8.GetString(actualBytes);

		logger.WriteLine($"Expected: {expected}");
		logger.WriteLine($"Actual:   {actual}");

		Assert.Equal(expected, actual);
	}
}
