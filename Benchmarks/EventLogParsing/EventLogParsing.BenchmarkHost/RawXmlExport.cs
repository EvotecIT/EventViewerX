using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Xml;

namespace EventLogParsing.BenchmarkHost;

internal static class RawXmlExport {
    internal static ExportMeasurement Run(BenchmarkOptions options) {
        string outputPath = options.OutputPath ??
            throw new InvalidOperationException("Raw XML export requires an output path.");
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory)) {
            Directory.CreateDirectory(outputDirectory);
        }

        long count = 0;
        var query = new EventLogQuery(options.Path, PathType.FilePath, "*") {
            ReverseDirection = false,
            TolerateQueryErrors = false
        };
        using (var stream = new FileStream(
                   outputPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   1024 * 1024,
                   FileOptions.SequentialScan)) {
            using XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                CloseOutput = false,
                NewLineHandling = NewLineHandling.None
            });
            writer.WriteStartDocument();
            writer.WriteStartElement("Events");
            using var reader = new EventLogReader(query);
            while (options.MaxEvents == 0 || count < options.MaxEvents) {
                using EventRecord? record = reader.ReadEvent();
                if (record is null) {
                    break;
                }

                writer.WriteRaw(record.ToXml());
                count++;
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        var info = new FileInfo(outputPath);
        return new ExportMeasurement(outputPath, count, info.Length, null);
    }
}
