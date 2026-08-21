using System.Text.Json;

namespace EventLogParsing.BenchmarkHost;

internal static class Program {
    public static int Main(string[] args) {
        try {
            BenchmarkOptions options = BenchmarkOptions.Parse(args);
            System.Globalization.CultureInfo.CurrentUICulture = options.MessageCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = options.MessageCulture;
            BenchmarkResult result = EventEnumerationRunner.Run(options);
            string? directory = Path.GetDirectoryName(options.ResultPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                options.ResultPath,
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(
                $"{result.Engine}/{result.ReadMode}: {result.Count} events in {result.ElapsedMilliseconds:F3} ms");
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
