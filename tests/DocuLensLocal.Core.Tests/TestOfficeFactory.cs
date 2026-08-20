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

    public static string WriteXlsx(string directory, string fileName, string text, string? number = "1500")
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var escaped = System.Security.SecurityElement.Escape(text);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteZipText(zip, "xl/sharedStrings.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="1" uniqueCount="1">
              <si><t xml:space="preserve">{escaped}</t></si>
            </sst>
            """);
        WriteZipText(zip, "xl/worksheets/sheet1.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="1">
                  <c r="A1" t="s"><v>0</v></c>
                  <c r="B1"><v>{number}</v></c>
                </row>
              </sheetData>
            </worksheet>
            """);
        WriteZipText(zip, "xl/workbook.xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheets><sheet name="계약" sheetId="1" r:id="rId1" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"/></sheets>
            </workbook>
            """);
        return path;
    }

    public static string WriteXlsxRichRuns(string directory, string fileName, params string[] runs)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var siInner = string.Join(string.Empty, runs.Select(run =>
            $"<r><t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(run)}</t></r>"));
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteZipText(zip, "xl/sharedStrings.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="1" uniqueCount="1">
              <si>{siInner}</si>
            </sst>
            """);
        WriteZipText(zip, "xl/worksheets/sheet1.xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="1"><c r="A1" t="s"><v>0</v></c></row>
              </sheetData>
            </worksheet>
            """);
        WriteZipText(zip, "xl/workbook.xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"/></sheets>
            </workbook>
            """);
        return path;
    }

    public static string WriteXlsxWithImage(string directory, string fileName, string text, byte[] pngBytes)
    {
        var path = WriteXlsx(directory, fileName, text);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var image = zip.CreateEntry("xl/media/image1.png");
        using var stream = image.Open();
        stream.Write(pngBytes);
        return path;
    }

    public static string WriteLegacyXls(string directory, string fileName, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var payload = Encoding.Unicode.GetBytes(text);
        var record = new byte[4 + 8 + 3 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0), 0x00FC);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(2), (ushort)(8 + 3 + payload.Length));
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(12), (ushort)text.Length);
        record[14] = 0x01;
        payload.CopyTo(record.AsSpan(15));

        using (var root = RootStorage.Create(path))
        {
            using var stream = root.CreateStream("Workbook");
            stream.Write(record);
        }

        return path;
    }

    private static void WriteZipText(ZipArchive zip, string entryName, string xml)
    {
        var entry = zip.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
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

    public static string WriteDocxWithImage(string directory, string fileName, string text, byte[] pngBytes)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var document = zip.CreateEntry("word/document.xml");
        using (var writer = new StreamWriter(document.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body><w:p><w:r><w:t xml:space="preserve">
                """);
            writer.Write(System.Security.SecurityElement.Escape(text));
            writer.Write("</w:t></w:r></w:p></w:body></w:document>");
        }

        var image = zip.CreateEntry("word/media/image1.png");
        using (var stream = image.Open())
        {
            stream.Write(pngBytes);
        }

        return path;
    }

    public static string WriteHwpxWithImage(string directory, string fileName, string text, byte[] pngBytes)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var section = zip.CreateEntry("Contents/section0.xml");
        using (var writer = new StreamWriter(section.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <hs:sec xmlns:hs="http://www.hancom.co.kr/hwpml/2011/section" xmlns:hp="http://www.hancom.co.kr/hwpml/2011/paragraph">
                  <hp:p><hp:run><hp:t>
                """);
            writer.Write(System.Security.SecurityElement.Escape(text));
            writer.Write("</hp:t></hp:run></hp:p></hs:sec>");
        }

        var image = zip.CreateEntry("BinData/image1.png");
        using (var stream = image.Open())
        {
            stream.Write(pngBytes);
        }

        return path;
    }

    public static string WriteHwpWithImage(string directory, string fileName, string text, byte[] pngBytes)
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
        if (!string.IsNullOrWhiteSpace(text))
        {
            paraText.AddString(text);
        }

        hwp.BinData.AddNewEmbeddedBinaryData("BIN0001.png", pngBytes, HwpLib.Object.DocInfo.BinData.BinDataCompress.ByStorageDefault);
        HWPWriter.ToFile(hwp, path);
        return path;
    }
}
