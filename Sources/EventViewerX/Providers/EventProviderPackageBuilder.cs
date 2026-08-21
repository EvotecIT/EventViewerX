using System.IO.Compression;
using System.Text.Json;

namespace EventViewerX.Providers;

/// <summary>
/// Builds portable, SDK-free-at-deployment provider packages from typed
/// definitions.
/// </summary>
public static class EventProviderPackageBuilder {
    /// <summary>
    /// Validates, compatibility-checks, compiles, hashes, and packages one
    /// provider definition.
    /// </summary>
    public static EventProviderPackageBuildResult Build(
        EventProviderDefinition definition,
        string outputPath,
        EventProviderPackageBuildOptions? options = null) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (string.IsNullOrWhiteSpace(outputPath)) {
            throw new ArgumentException(
                "Output path cannot be empty.",
                nameof(outputPath));
        }
        options ??= new EventProviderPackageBuildOptions();
        EventProviderValidationResult validation =
            EventProviderDefinitionValidator.ValidateOrThrow(definition);
        if (!string.IsNullOrWhiteSpace(options.BaselinePath)) {
            EventProviderDefinition baseline =
                LoadBaseline(options.BaselinePath);
            EventProviderCompatibility.EnsureCompatible(
                baseline,
                definition);
        }

        string finalPath = Path.GetFullPath(outputPath);
        string? outputDirectory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory)) {
            Directory.CreateDirectory(outputDirectory);
        }
        if (File.Exists(finalPath) && !options.Overwrite) {
            throw new IOException(
                $"Provider package '{finalPath}' already exists.");
        }

        string buildRoot = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX",
            "ProviderBuild",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(buildRoot);
        string temporaryPackage = finalPath + "." +
                                  Guid.NewGuid().ToString("N") +
                                  ".tmp";
        try {
            BuildContents(
                definition,
                buildRoot);
            EventProviderPackageManifest packageManifest =
                CreatePackageManifest(
                    definition,
                    buildRoot);
            if (options.SigningCertificate != null) {
                EventProviderPackageSignature.Sign(
                    packageManifest,
                    options.SigningCertificate);
            }
            File.WriteAllText(
                Path.Combine(
                    buildRoot,
                    EventProviderPackageLayout
                        .PackageManifestFileName),
                JsonSerializer.Serialize(
                    packageManifest,
                    EventProviderDefinitionJson.SerializerOptions),
                new UTF8Encoding(false));
            CreateArchive(
                buildRoot,
                temporaryPackage);
            Promote(
                temporaryPackage,
                finalPath,
                options.Overwrite);

            return new EventProviderPackageBuildResult {
                OutputPath = finalPath,
                ProviderName = definition.Name,
                ProviderId = definition.Id,
                PackageVersion = definition.PackageVersion,
                PackageSha256 =
                    EventProviderHash.FileSha256(finalPath),
                ResourceSha256 =
                    packageManifest.Files[
                        EventProviderPackageLayout
                            .ResourceFileName],
                Compiler = EventProviderManagedCompiler.Name,
                Warnings = validation.Warnings,
                IsSigned = options.SigningCertificate != null,
                SignerThumbprint =
                    options.SigningCertificate?.Thumbprint ??
                    string.Empty
            };
        } finally {
            CleanupTemporaryArtifacts(
                temporaryPackage,
                buildRoot);
        }
    }

    /// <summary>
    /// Removes build-owned temporary artifacts without replacing the build
    /// outcome when cleanup itself fails.
    /// </summary>
    internal static void CleanupTemporaryArtifacts(
        string temporaryPackage,
        string buildRoot,
        Action<string>? deleteFile = null,
        Action<string, bool>? deleteDirectory = null) {

        deleteFile ??= File.Delete;
        deleteDirectory ??= Directory.Delete;
        try {
            if (File.Exists(temporaryPackage)) {
                deleteFile(temporaryPackage);
            }
        } catch (Exception) {
        }
        try {
            if (Directory.Exists(buildRoot)) {
                deleteDirectory(
                    buildRoot,
                    true);
            }
        } catch (Exception) {
        }
    }

    private static void BuildContents(
        EventProviderDefinition definition,
        string buildRoot) {

        string definitionPath = Path.Combine(
            buildRoot,
            EventProviderPackageLayout.DefinitionFileName);
        string manifestPath = Path.Combine(
            buildRoot,
            EventProviderPackageLayout.ManifestFileName);
        string schemaLockPath = Path.Combine(
            buildRoot,
            EventProviderPackageLayout.SchemaLockFileName);
        string resourcePath = Path.Combine(
            buildRoot,
            EventProviderPackageLayout.ResourceFileName);

        File.WriteAllText(
            definitionPath,
            EventProviderDefinitionJson.Serialize(definition),
            new UTF8Encoding(false));
        File.WriteAllText(
            schemaLockPath,
            EventProviderSchemaLock.Create(definition).ToJson(),
            new UTF8Encoding(false));
        File.WriteAllText(
            manifestPath,
            EventProviderManifestGenerator.Generate(
                definition,
                EventProviderPackageLayout.ResourceFileName),
            new UTF8Encoding(false));

        EventProviderManagedCompiler.Compile(definition, resourcePath);
        if (!File.Exists(resourcePath) ||
            new FileInfo(resourcePath).Length == 0) {
            throw new InvalidDataException(
                "Provider resource DLL was not produced.");
        }
    }

    private static EventProviderPackageManifest CreatePackageManifest(
        EventProviderDefinition definition,
        string buildRoot) {

        var files = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        foreach (string fileName in new[] {
                     EventProviderPackageLayout.DefinitionFileName,
                     EventProviderPackageLayout.ManifestFileName,
                     EventProviderPackageLayout.ResourceFileName,
                     EventProviderPackageLayout.SchemaLockFileName
                 }) {
            files[fileName] = EventProviderHash.FileSha256(
                Path.Combine(buildRoot, fileName));
        }
        return new EventProviderPackageManifest {
            FormatVersion = 2,
            ProviderName = definition.Name,
            ProviderId = definition.Id,
            PackageVersion = definition.PackageVersion,
            Compiler = EventProviderManagedCompiler.Name,
            CompilerVersion = EventProviderManagedCompiler.Version,
            Files = files
        };
    }

    private static void CreateArchive(
        string buildRoot,
        string packagePath) {

        using FileStream output = new(
            packagePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);
        using var archive = new ZipArchive(
            output,
            ZipArchiveMode.Create,
            leaveOpen: false);
        foreach (string sourcePath in Directory
                     .EnumerateFiles(
                         buildRoot,
                         "*",
                         SearchOption.TopDirectoryOnly)
                     .OrderBy(
                         static path => Path.GetFileName(path),
                         StringComparer.Ordinal)) {
            string fileName = Path.GetFileName(sourcePath);
            ZipArchiveEntry entry = archive.CreateEntry(
                fileName,
                CompressionLevel.Optimal);
            entry.LastWriteTime =
                EventProviderPackageLayout.StableZipTimestamp;
            using Stream destination = entry.Open();
            using FileStream source = File.OpenRead(sourcePath);
            source.CopyTo(destination);
        }
    }

    private static void Promote(
        string temporaryPath,
        string finalPath,
        bool overwrite) {

        if (File.Exists(finalPath)) {
            if (!overwrite) {
                throw new IOException(
                    $"Provider package '{finalPath}' already exists.");
            }
            File.Replace(
                temporaryPath,
                finalPath,
                null);
        } else {
            try {
                File.Move(
                    temporaryPath,
                    finalPath);
            } catch (IOException) when (
                overwrite &&
                File.Exists(finalPath)) {
                File.Replace(
                    temporaryPath,
                    finalPath,
                    null);
            }
        }
    }

    private static EventProviderDefinition LoadBaseline(string path) {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) {
            throw new FileNotFoundException(
                "Provider compatibility baseline was not found.",
                fullPath);
        }
        if (string.Equals(
                Path.GetExtension(fullPath),
                ".evxprovider",
                StringComparison.OrdinalIgnoreCase)) {
            using EventProviderPackage package =
                EventProviderPackageReader.Open(fullPath);
            return package.Definition;
        }
        return EventProviderDefinitionJson.Load(fullPath);
    }
}
