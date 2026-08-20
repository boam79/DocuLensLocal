using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using HwpLib.Tool.BlankFileMaker;
using HwpLib.Writer;
using OpenMcdf;

namespace DocuLensLocal.Core.Tests;

internal static class TestOfficeFactory
{
    public static string WriteDocx(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("word/document.xml");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body><w:p><w:r><w:t xml:space="preserve">
            """);
        writer.Write(System.Security.SecurityElement.Escape(text));
        writer.Write("</w:t></w:r></w:p></w:body></w:document>");
        return path;
    }

    public static string WriteHwpx(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("Contents/section0.xml");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section" xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
              <hp:p><hp:run><hp:t>
            """);
        writer.Write(System.Security.SecurityElement.Escape(text));
        writer.Write("</hp:t></hp:run></hp:p></hs:sec>");
        return path;
    }

    public static string WriteHwp(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var hwp = BlankFileMaker.Make();
        var paragraph = hwp.BodyText.SectionList[0].GetParagraph(0);
        if (paragraph.Text is null)
        {
            paragraph.CreateText();
        }

        var paraText = paragraph.Text ?? throw new InvalidOperationException("HWP paragraph text was not created.");
        paraText.AddString(text);
        HWPWriter.ToFile(hwp, path);
        return path;
    }

    public static string WriteLegacyDoc(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var payload = Encoding.Unicode.GetBytes(text + "\r");
        var fib = new byte[0x200];
        BinaryPrimitives.WriteUInt16LittleEndian(fib.AsSpan(0), 0xA5EC);
        BinaryPrimitives.WriteUInt16LittleEndian(fib.AsSpan(2), 0x00C1);
        BinaryPrimitives.WriteInt32LittleEndian(fib.AsSpan(0x18), 0x200);
        BinaryPrimitives.WriteInt32LittleEndian(fib.AsSpan(0x1C), 0x200 + payload.Length);

        using (var root = RootStorage.Create(path))
        {
            using var stream = root.CreateStream("WordDocument");
            stream.Write(fib);
            stream.Write(payload);
        }

        return path;
    }
}
