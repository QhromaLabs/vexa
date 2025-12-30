using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Vexa.Models;

namespace Vexa.Services;

public sealed class ExportService
{
    private readonly SrtService _srtService = new();

    public void ExportSrt(string path, IEnumerable<Segment> segments)
    {
        File.WriteAllText(path, _srtService.Build(segments));
    }

    public void ExportVtt(string path, IEnumerable<Segment> segments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WEBVTT");
        builder.AppendLine();
        foreach (var segment in segments)
        {
            builder.AppendLine($"{FormatVttTime(segment.StartTime)} --> {FormatVttTime(segment.EndTime)}");
            builder.AppendLine(segment.Text.Trim());
            builder.AppendLine();
        }

        File.WriteAllText(path, builder.ToString());
    }

    public void ExportTxt(string path, IEnumerable<Segment> segments)
    {
        File.WriteAllText(path, BuildText(segments));
    }

    public void ExportDocx(string path, IEnumerable<Segment> segments)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        AddEntry(archive, "[Content_Types].xml", GetContentTypesXml());
        AddEntry(archive, "_rels/.rels", GetRootRelsXml());
        AddEntry(archive, "word/document.xml", BuildDocXml(segments));
        AddEntry(archive, "word/_rels/document.xml.rels", GetDocRelsXml());
    }

    private static string BuildText(IEnumerable<Segment> segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            var header = string.IsNullOrWhiteSpace(segment.Speaker)
                ? $"[{FormatClock(segment.StartTime)} - {FormatClock(segment.EndTime)}]"
                : $"[{segment.Speaker} | {FormatClock(segment.StartTime)} - {FormatClock(segment.EndTime)}]";
            builder.AppendLine(header);
            builder.AppendLine(segment.Text.Trim());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildDocXml(IEnumerable<Segment> segments)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8,
            Indent = false
        };

        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = XmlWriter.Create(stringWriter, settings);

        writer.WriteStartDocument(true);
        writer.WriteStartElement("w", "document", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
        writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        writer.WriteStartElement("w", "body", null);

        foreach (var segment in segments)
        {
            WriteParagraph(writer, string.IsNullOrWhiteSpace(segment.Speaker)
                ? $"[{FormatClock(segment.StartTime)} - {FormatClock(segment.EndTime)}]"
                : $"[{segment.Speaker} | {FormatClock(segment.StartTime)} - {FormatClock(segment.EndTime)}]");

            WriteParagraph(writer, segment.Text.Trim());
            WriteParagraph(writer, string.Empty);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        return stringWriter.ToString();
    }

    private static void WriteParagraph(XmlWriter writer, string text)
    {
        writer.WriteStartElement("w", "p", null);
        writer.WriteStartElement("w", "r", null);
        writer.WriteStartElement("w", "t", null);
        writer.WriteString(text);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string GetContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "</Types>";

    private static string GetRootRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";

    private static string GetDocRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"></Relationships>";

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string FormatClock(TimeSpan span)
    {
        return span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatVttTime(TimeSpan span)
    {
        return span.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }
}
