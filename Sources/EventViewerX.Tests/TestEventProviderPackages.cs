using EventViewerX.Providers;
using System.Diagnostics.Eventing.Reader;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using ProviderEventDefinition =
    EventViewerX.Providers.EventProviderEventDefinition;

namespace EventViewerX.Tests;

public sealed class TestEventProviderPackages {
    [Fact]
    public void GenericMetadataFailuresAreNotTreatedAsMissingRegistration() {
        Assert.False(
            EventProviderManifestRegistrar
                .IsMissingRegistrationFailure(
                    new EventLogException(
                        "Transient Event Log service failure.")));
    }

    [Fact]
    public void MissingProviderRegistrationIsConfirmedAsAbsent() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        Assert.False(
            EventProviderManifestRegistrar.IsRegistered(
                "EventViewerX.Tests.Missing." +
                Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void StagingCleanupCannotReplaceTheActivationOutcome() {
        string staging =
            Path.Combine(
                Path.GetTempPath(),
                "EventViewerX.Tests",
                Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try {
            EventProviderPackageManager.CleanupStagingDirectory(
                staging,
                static (_, _) =>
                    throw new IOException(
                        "Simulated antivirus lock."));
        } finally {
            Directory.Delete(
                staging,
                recursive: true);
        }
    }

    [Fact]
    public void OpeningADirectoryPreservesTheNativeAccessFailure() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            Assert.Throws<UnauthorizedAccessException>(() =>
                EventProviderPackageReader.Open(root));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ActivationPromotionReplacesAnExistingCorruptDirectory() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(root, "staging");
        string activation = Path.Combine(root, "active");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(activation);
        File.WriteAllText(
            Path.Combine(staging, "new.txt"),
            "new");
        File.WriteAllText(
            Path.Combine(activation, "old.txt"),
            "old");
        try {
            EventProviderPackageManager
                .PromoteActivationDirectory(
                    staging,
                    activation);

            Assert.True(
                File.Exists(
                    Path.Combine(activation, "new.txt")));
            Assert.False(
                File.Exists(
                    Path.Combine(activation, "old.txt")));
            Assert.Empty(
                Directory.EnumerateDirectories(
                    root,
                    "active.replaced-*"));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ActivationPromotionRestoresTheExistingDirectoryWhenPromotionFails() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(root, "staging");
        string activation = Path.Combine(root, "active");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(activation);
        File.WriteAllText(
            Path.Combine(activation, "old.txt"),
            "old");
        int moves = 0;
        try {
            Assert.Throws<IOException>(() =>
                EventProviderPackageManager
                    .PromoteActivationDirectory(
                        staging,
                        activation,
                        (source, destination) => {
                            moves++;
                            if (moves == 2) {
                                throw new IOException(
                                    "simulated promotion failure");
                            }
                            Directory.Move(
                                source,
                                destination);
                        }));

            Assert.True(
                File.Exists(
                    Path.Combine(activation, "old.txt")));
            Assert.True(Directory.Exists(staging));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PackageBuildUsesTheManagedCompiler() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(
            root,
            "managed-compiler.evxprovider");
        try {
            EventProviderPackageBuildResult result =
                EventProviderPackageBuilder.Build(
                    CreateDefinition(),
                    packagePath);
            using EventProviderPackage package =
                EventProviderPackageReader.Open(packagePath);

            Assert.Equal(
                EventProviderManagedCompiler.Name,
                result.Compiler);
            Assert.Equal(2, package.Manifest.FormatVersion);
            Assert.Equal(
                EventProviderManagedCompiler.Name,
                package.Manifest.Compiler);
            Assert.Equal(
                EventProviderManagedCompiler.Version,
                package.Manifest.CompilerVersion);
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void OpensAndVerifiesSignedLegacyFormatOnePackages() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(
            root,
            "legacy-format-one.evxprovider");
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=EventViewerX Legacy Package Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate =
            request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1));
        try {
            EventProviderPackageBuilder.Build(
                CreateDefinition(),
                packagePath);
            using (ZipArchive archive = ZipFile.Open(
                       packagePath,
                       ZipArchiveMode.Update)) {
                ZipArchiveEntry entry = archive.GetEntry(
                    EventProviderPackageLayout
                        .PackageManifestFileName)!;
                JsonObject manifest;
                using (Stream stream = entry.Open()) {
                    manifest = JsonNode.Parse(stream)!.AsObject();
                }
                manifest["formatVersion"] = 1;
                manifest.Remove("compiler");
                manifest.Remove("compilerVersion");
                manifest["windowsSdkVersion"] = "10.0.26100.0";
                manifest["msvcVersion"] = "14.42.34433";
                EventProviderPackageManifest legacyManifest =
                    JsonSerializer.Deserialize<EventProviderPackageManifest>(
                        manifest.ToJsonString(),
                        EventProviderDefinitionJson.SerializerOptions)!;
                EventProviderPackageSignature.Sign(
                    legacyManifest,
                    certificate);
                ReplaceEntry(
                    entry,
                    JsonSerializer.SerializeToUtf8Bytes(
                        legacyManifest,
                        EventProviderDefinitionJson.SerializerOptions));
            }

            using EventProviderPackage package =
                EventProviderPackageReader.Open(packagePath);
            Assert.Equal(1, package.Manifest.FormatVersion);
            Assert.True(package.IsSigned);
            Assert.Equal(
                certificate.Thumbprint,
                package.SignerCertificate?.Thumbprint);
            Assert.Equal(
                "Evotec-EventViewerX-PackageTest",
                package.Manifest.ProviderName);
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RejectsExpandedEntryBytesBeyondTheDeclaredBudget() {
        long expandedBytes = 0;
        using var input = new MemoryStream(
            Enumerable.Range(0, 11)
                .Select(static value => (byte)value)
                .ToArray());

        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() =>
                EventProviderPackageReader.ReadBounded(
                    input,
                    "payload.bin",
                    maximumEntryBytes: 10,
                    maximumPackageBytes: 100,
                    ref expandedBytes));

        Assert.Contains(
            "expanded bytes",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsExpandedPackageBytesAcrossEntries() {
        long expandedBytes = 9;
        using var input = new MemoryStream(
            new byte[] { 1, 2 });

        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(() =>
                EventProviderPackageReader.ReadBounded(
                    input,
                    "payload.bin",
                    maximumEntryBytes: 10,
                    maximumPackageBytes: 10,
                    ref expandedBytes));

        Assert.Contains(
            "expanded contents",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsANullPackageFileMapAsInvalidData() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string packagePath = Path.Combine(
            root,
            "null-files.evxprovider");
        try {
            using (ZipArchive archive = ZipFile.Open(
                       packagePath,
                       ZipArchiveMode.Create)) {
                ZipArchiveEntry entry =
                    archive.CreateEntry(
                        EventProviderPackageLayout
                            .PackageManifestFileName);
                using var writer =
                    new StreamWriter(
                        entry.Open(),
                        new UTF8Encoding(false));
                writer.Write(
                    "{\"formatVersion\":2,\"files\":null}");
            }

            InvalidDataException exception =
                Assert.Throws<InvalidDataException>(() =>
                    EventProviderPackageReader.Open(
                        packagePath));

            Assert.Contains(
                "file map",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        } finally {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void GeneratesNamedMessagesAndStableFields() {
        EventProviderDefinition definition = CreateDefinition();

        string manifest = EventProviderManifestGenerator.Generate(
            definition,
            "provider.resources.dll");

        Assert.Contains("name=\"ComputerName\"", manifest);
        Assert.Contains("name=\"FindingCount\"", manifest);
        Assert.Contains(
            "Scan of %1 found %2 issues.",
            manifest);
    }

    [Fact]
    public void RejectsLiteralPercentSequencesBeforeResourceCompilation() {
        EventProviderDefinition definition = CreateDefinition();

        FormatException exception =
            Assert.Throws<FormatException>(() =>
                EventProviderMessageTemplateCompiler.Compile(
                    "Literal %1 and %0; field {ComputerName}; 100%.",
                    definition.Events[0].Fields));

        Assert.Contains(
            "payload field",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratedManifestSymbolsContainOnlyAsciiIdentifiers() {
        Assert.Equal(
            "M_NCHEN_1",
            EventProviderManifestNames.Symbol(
                "München-1",
                "Fallback"));
    }

    [Fact]
    public void RejectsUndefinedMapKindsDuringValidation() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Maps.Add(
            new EventProviderMapDefinition {
                Name = "Unsupported",
                Kind = (EventProviderMapKind)int.MaxValue
            });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "MapKindInvalid" &&
                     issue.Path == "Maps[0].Kind");
    }

    [Fact]
    public void DuplicateFieldsReturnValidationIssuesWithoutThrowing() {
        EventProviderDefinition definition =
            CreateDefinition();
        EventProviderEventDefinition eventDefinition =
            definition.Events[0];
        eventDefinition.Fields.Add(
            EventProviderFieldDefinition.Create(
                eventDefinition.Fields[0].Name,
                EventProviderFieldType.UnicodeString));

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code == "DuplicateFieldName");
    }

    [Fact]
    public void RejectsMapNamesThatAreNotManifestIdentifiers() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Maps.Add(
            new EventProviderMapDefinition {
                Name = "Result Map"
            });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "MapNameInvalid" &&
                     issue.Path == "Maps[0].Name");
    }

    [Fact]
    public void RejectsCustomMetadataNamesThatAreNotManifestIdentifiers() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Levels.Add(
            new EventProviderLevelDefinition {
                Name = "Custom Level",
                Value = 16
            });
        definition.Tasks.Add(
            new EventProviderTaskDefinition {
                Name = "Custom Task",
                Value = 1,
                Opcodes = {
                    new EventProviderOpcodeDefinition {
                        Name = "Task Opcode",
                        Value = 10
                    }
                }
            });
        definition.Opcodes.Add(
            new EventProviderOpcodeDefinition {
                Name = "Global Opcode",
                Value = 11
            });
        definition.Keywords.Add(
            new EventProviderKeywordDefinition {
                Name = "Custom Keyword",
                Mask = 1
            });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "LevelNameInvalid" &&
                     issue.Path == "Levels[0].Name");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "TaskNameInvalid" &&
                     issue.Path == "Tasks[0].Name");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "OpcodeNameInvalid" &&
                     issue.Path == "Tasks[0].Opcodes[0].Name");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "OpcodeNameInvalid" &&
                     issue.Path == "Opcodes[0].Name");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "KeywordNameInvalid" &&
                     issue.Path == "Keywords[0].Name");
    }

    [Fact]
    public void RejectsMapValuesOutsideTheWindowsUInt32Range() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Maps.Add(
            new EventProviderMapDefinition {
                Name = "Values",
                Entries = {
                    new EventProviderMapEntryDefinition {
                        Value = -1
                    },
                    new EventProviderMapEntryDefinition {
                        Value = (long)uint.MaxValue + 1
                    }
                }
            });
        definition.Maps.Add(
            new EventProviderMapDefinition {
                Name = "Bits",
                Kind = EventProviderMapKind.Bit,
                Entries = {
                    new EventProviderMapEntryDefinition {
                        Value = 1L << 32
                    }
                }
            });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Equal(
            3,
            result.Issues.Count(
                issue => issue.Code ==
                         "MapValueOutOfRange"));
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code ==
                     "BitMapValueInvalid" &&
                     issue.Path ==
                     "Maps[1].Entries[0].Value");
    }

    [Fact]
    public void RejectsDuplicateOpcodeValuesWithinEachManifestScope() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Opcodes.Add(
            new EventProviderOpcodeDefinition {
                Name = "ProviderOne",
                Value = 10
            });
        definition.Opcodes.Add(
            new EventProviderOpcodeDefinition {
                Name = "ProviderTwo",
                Value = 10
            });
        definition.Tasks.Add(
            new EventProviderTaskDefinition {
                Name = "Task",
                Value = 1,
                Opcodes = {
                    new EventProviderOpcodeDefinition {
                        Name = "TaskOne",
                        Value = 11
                    },
                    new EventProviderOpcodeDefinition {
                        Name = "TaskTwo",
                        Value = 11
                    }
                }
            });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "DuplicateOpcodeValue" &&
                     issue.Path == "Opcodes");
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "DuplicateOpcodeValue" &&
                     issue.Path ==
                         "Tasks[0].Opcodes");
    }

    [Fact]
    public void UninstallRecoveryAcceptsUnreadableOrMismatchedPayloads() {
        Assert.True(
            EventProviderPackageManager
                .IsRecoverableActivePayloadFailure(
                    new InvalidDataException()));
        Assert.True(
            EventProviderPackageManager
                .IsRecoverableActivePayloadFailure(
                    new IOException()));
        Assert.True(
            EventProviderPackageManager
                .IsRecoverableActivePayloadFailure(
                    new UnauthorizedAccessException()));
        Assert.False(
            EventProviderPackageManager
                .IsRecoverableActivePayloadFailure(
                    new ArgumentException()));
    }

    [Fact]
    public void RejectsUndefinedChannelEnumsDuringValidation() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Channels[0].Type =
            (EventProviderChannelType)int.MaxValue;
        definition.Channels[0].Isolation =
            (EventProviderChannelIsolation)int.MaxValue;

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "ChannelTypeInvalid" &&
                     issue.Path == "Channels[0].Type");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "ChannelIsolationInvalid" &&
                     issue.Path == "Channels[0].Isolation");
    }

    [Fact]
    public void RejectsUndefinedFieldEnumsDuringValidation() {
        EventProviderDefinition definition = CreateDefinition();
        definition.Events[0].Fields[0].Type =
            (EventProviderFieldType)int.MaxValue;
        definition.Events[0].Fields[0].OutputType =
            (EventProviderFieldOutputType)int.MaxValue;

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "FieldTypeInvalid" &&
                     issue.Path == "Events[0].Fields[0].Type");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "FieldOutputTypeInvalid" &&
                     issue.Path ==
                     "Events[0].Fields[0].OutputType");
    }

    [Theory]
    [InlineData("Invalid Type")]
    [InlineData("custom:Value")]
    [InlineData("win:")]
    [InlineData("win:Value:Extra")]
    [InlineData("win:NotARealType")]
    [InlineData("xs:notAType")]
    public void RejectsInvalidOrUndeclaredCustomOutputTypeNames(
        string customOutputType) {

        EventProviderDefinition definition =
            CreateDefinition();
        definition.Events[0].Fields[0]
            .CustomOutputType =
                customOutputType;

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "FieldCustomOutputTypeInvalid" &&
                     issue.Path ==
                         "Events[0].Fields[0].CustomOutputType");
    }

    [Theory]
    [InlineData("xs:string")]
    [InlineData("win:Xml")]
    public void AcceptsCustomOutputTypesFromDeclaredNamespaces(
        string customOutputType) {

        EventProviderDefinition definition =
            CreateDefinition();
        definition.Events[0].Fields[0]
            .CustomOutputType =
                customOutputType;

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code ==
                     "FieldCustomOutputTypeInvalid");
    }

    [Fact]
    public void AcceptsCustomOutputTypeCompatibleWithNumericInput() {
        EventProviderDefinition definition =
            CreateDefinition();
        definition.Events[0].Fields[1]
            .CustomOutputType =
                "win:HexInt32";

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code ==
                     "FieldCustomOutputTypeInvalid");
    }

    [Fact]
    public void RejectsKnownOutputTypeWhenItIsIncompatibleWithInput() {
        EventProviderDefinition definition =
            CreateDefinition();
        definition.Events[0].Fields[0]
            .CustomOutputType =
                "win:HexInt32";

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "FieldCustomOutputTypeInvalid" &&
                     issue.Path ==
                         "Events[0].Fields[0].CustomOutputType");
    }

    [Fact]
    public void ValidationRejectsAnInvalidGeneratedFallbackMessage() {
        EventProviderDefinition definition =
            CreateDefinition();
        definition.Events[0].Name =
            "CPU 100%";
        definition.Events[0].Messages.Clear();

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "EventFallbackMessageInvalid" &&
                     issue.Path ==
                         "Events[0].FallbackMessage");
    }

    [Fact]
    public void RejectsInvalidCulturesAcrossLocalizedProviderValues() {
        const string invalidCulture = "not_a_real_culture";
        EventProviderDefinition definition = CreateDefinition();
        definition.DisplayNames[invalidCulture] = "Provider";
        definition.Channels[0].DisplayNames[invalidCulture] =
            "Operational";
        definition.Maps.Add(
            new EventProviderMapDefinition {
                Name = "Results",
                Entries = {
                    new EventProviderMapEntryDefinition {
                        Value = 1,
                        Messages = {
                            [invalidCulture] = "Success"
                        }
                    }
                }
            });

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "LocalizationCultureInvalid" &&
                     issue.Path ==
                         $"DisplayNames[{invalidCulture}]");
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "LocalizationCultureInvalid" &&
                     issue.Path ==
                         $"Channels[0].DisplayNames[{invalidCulture}]");
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                         "LocalizationCultureInvalid" &&
                     issue.Path ==
                         $"Maps[0].Entries[0].Messages[{invalidCulture}]");
    }

    [Fact]
    public void RejectsSchemaChangesWithoutAnEventVersionBump() {
        EventProviderDefinition baseline = CreateDefinition();
        EventProviderDefinition candidate = CreateDefinition();
        candidate.Events[0].Fields[1].Type =
            EventProviderFieldType.UInt64;

        EventProviderCompatibilityResult result =
            EventProviderCompatibility.Compare(
                baseline,
                candidate);

        Assert.False(result.IsCompatible);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "EventFieldTypeChanged");
    }

    [Fact]
    public void RejectsCaseOnlyFieldNameChangesWithoutAnEventVersionBump() {
        EventProviderDefinition baseline = CreateDefinition();
        EventProviderDefinition candidate = CreateDefinition();
        candidate.Events[0].Fields[0].Name =
            "computerName";

        EventProviderCompatibilityResult result =
            EventProviderCompatibility.Compare(
                baseline,
                candidate);

        Assert.False(result.IsCompatible);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     "EventFieldNameChanged");
    }

    [Fact]
    public void InfersTypedPayloadsAndRequiresExplicitArrayDimensions() {
        IReadOnlyList<EventProviderFieldDefinition> fields =
            EventProviderTypedPayload.Describe<ScanCompletedPayload>();

        Assert.Collection(
            fields,
            field => {
                Assert.Equal("ComputerName", field.Name);
                Assert.Equal(
                    EventProviderFieldType.UnicodeString,
                    field.Type);
            },
            field => {
                Assert.Equal("FindingCount", field.Name);
                Assert.Equal(
                    EventProviderFieldType.UInt32,
                    field.Type);
            });
        Assert.Throws<ArgumentException>(() =>
            EventProviderTypedPayload.Describe<InvalidArrayPayload>());
    }

    [Fact]
    public void BuildsOpensAndVerifiesSignedProviderPackage() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string packagePath = Path.Combine(
            root,
            "smoke.evxprovider");
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=EventViewerX Package Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate =
            request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1));
        try {
            EventProviderDefinition definition =
                CreateDefinition();
            EventProviderPackageBuildResult result =
                EventProviderPackageBuilder.Build(
                    definition,
                    packagePath,
                    new EventProviderPackageBuildOptions {
                        SigningCertificate = certificate
                    });
            using EventProviderPackage package =
                EventProviderPackageReader.Open(packagePath);

            Assert.True(result.IsSigned);
            Assert.Equal(
                certificate.Thumbprint,
                result.SignerThumbprint);
            Assert.True(package.IsSigned);
            Assert.Equal(
                certificate.Thumbprint,
                package.SignerCertificate?.Thumbprint);
            Assert.Equal(
                "Evotec-EventViewerX-PackageTest",
                package.Definition.Name);
            Assert.Equal(4, package.Manifest.Files.Count);

            EventProviderPackageTrust.EnsureAllowed(
                package,
                new EventProviderPackageInstallOptions {
                    TrustMode = EventProviderPackageTrustMode
                        .RequireTrustedSignature,
                    TrustedSignerThumbprints =
                        new[] { certificate.Thumbprint }
                });
            InvalidDataException wrongSigner =
                Assert.Throws<InvalidDataException>(() =>
                    EventProviderPackageTrust.EnsureAllowed(
                        package,
                        new EventProviderPackageInstallOptions {
                            TrustMode = EventProviderPackageTrustMode
                                .RequireTrustedSignature,
                            TrustedSignerThumbprints =
                                new[] {
                                    "0000000000000000000000000000000000000000"
                                }
                        }));
            Assert.Contains(
                "allowlist",
                wrongSigner.Message,
                StringComparison.OrdinalIgnoreCase);
            InvalidDataException missingCodeSigning =
                Assert.Throws<InvalidDataException>(() =>
                    EventProviderPackageTrust.EnsureAllowed(
                        package,
                        new EventProviderPackageInstallOptions {
                            TrustMode = EventProviderPackageTrustMode
                                .RequireTrustedSignature
                        }));
            Assert.Contains(
                "code signing",
                missingCodeSigning.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EventProviderPackageTrust.EnsureAllowed(
                    package,
                    new EventProviderPackageInstallOptions {
                        TrustMode =
                            (EventProviderPackageTrustMode)int.MaxValue
                    }));

            string extractedPath = Path.Combine(
                root,
                "extracted");
            using (EventProviderPackage extracted =
                   EventProviderPackageReader.Extract(
                       packagePath,
                       extractedPath)) {
            }
            File.WriteAllText(
                Path.Combine(
                    extractedPath,
                    "provider.definition.json"),
                "{}",
                new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(() =>
                EventProviderPackageReader
                    .EnsureExtractedFilesMatch(
                        packagePath,
                        extractedPath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(
        "{\"name\":null,\"id\":\"520ecea3-f786-459c-8b02-2a288cbef31c\",\"packageVersion\":\"1.0.0\"}",
        "ProviderNameRequired",
        "Name")]
    [InlineData(
        "{\"name\":\"Provider\",\"id\":\"520ecea3-f786-459c-8b02-2a288cbef31c\",\"packageVersion\":null}",
        "PackageVersionRequired",
        "PackageVersion")]
    [InlineData(
        "{\"name\":\"Provider\",\"id\":\"520ecea3-f786-459c-8b02-2a288cbef31c\",\"packageVersion\":\"1.0.0\",\"channels\":null}",
        "DefinitionMemberNull",
        "Channels")]
    [InlineData(
        "{\"name\":\"Provider\",\"id\":\"520ecea3-f786-459c-8b02-2a288cbef31c\",\"packageVersion\":\"1.0.0\",\"events\":[null]}",
        "DefinitionMemberNull",
        "Events[0]")]
    public void NullJsonMembersProduceStructuredValidation(
        string json,
        string code,
        string path) {

        EventProviderValidationException exception =
            Assert.Throws<EventProviderValidationException>(() =>
                EventProviderDefinitionJson.Parse(json));

        Assert.Contains(
            exception.Result.Issues,
            issue => issue.Code == code &&
                     issue.Path == path);
    }

    [Fact]
    public void RejectsUnknownDefinitionPropertiesAtEverySchemaLevel() {
        string json = EventProviderDefinitionJson.Serialize(
            CreateDefinition());

        Assert.Throws<JsonException>(() =>
            EventProviderDefinitionJson.Parse(
                json.Replace(
                    "{",
                    "{\"packageVerzion\":\"1.0.0\",",
                    StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() =>
            EventProviderDefinitionJson.Parse(
                json.Replace(
                    "\"fields\": [",
                    "\"fieldz\": true, \"fields\": [",
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void DefinitionPromotionWithoutOverwritePreservesCompetingFile() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string temporaryPath = Path.Combine(root, "definition.tmp");
        string destinationPath = Path.Combine(root, "definition.json");
        try {
            File.WriteAllText(temporaryPath, "new-content");
            File.WriteAllText(destinationPath, "competing-content");

            Assert.Throws<IOException>(() =>
                EventProviderDefinitionJson.PromoteTemporaryFile(
                    temporaryPath,
                    destinationPath,
                    overwrite: false));

            Assert.Equal(
                "competing-content",
                File.ReadAllText(destinationPath));
            Assert.True(File.Exists(temporaryPath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinitionTemporaryCleanupIsBestEffort(
        bool accessDenied) {

        EventProviderDefinitionJson
            .DeleteTemporaryBestEffort(
                "definition.tmp",
                _ => {
                    if (accessDenied) {
                        throw new UnauthorizedAccessException(
                            "cleanup denied");
                    }
                    throw new IOException(
                        "cleanup busy");
                });
    }

    [Fact]
    public void ActivationPromotionReplacesAnIncompleteExistingDirectory() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(
            root,
            "staging");
        string activation = Path.Combine(
            root,
            "activation");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(activation);
        File.WriteAllText(
            Path.Combine(staging, "new.txt"),
            "new");
        File.WriteAllText(
            Path.Combine(activation, "old.txt"),
            "old");
        try {
            EventProviderPackageManager
                .PromoteActivationDirectory(
                    staging,
                    activation);

            Assert.False(Directory.Exists(staging));
            Assert.True(
                File.Exists(
                    Path.Combine(
                        activation,
                        "new.txt")));
            Assert.False(
                File.Exists(
                    Path.Combine(
                        activation,
                        "old.txt")));
            Assert.Empty(
                Directory.GetDirectories(
                    root,
                    "activation.replaced-*"));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingPackagePreservesFileStreamFailureSemantics() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string missing = Path.Combine(
            root,
            "missing.evxprovider");
        try {
            FileNotFoundException exception =
                Assert.Throws<FileNotFoundException>(() =>
                    EventProviderPackageReader.Open(
                        missing));

            Assert.Equal(
                Path.GetFullPath(missing),
                exception.FileName);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsNamesThatCollideAfterManifestNormalization() {
        EventProviderDefinition definition = CreateDefinition();
        definition.AddChannel(
            EventProviderChannelDefinition.Operational(
                "A-B",
                definition.Name + "/A-B"));
        definition.AddChannel(
            EventProviderChannelDefinition.Operational(
                "A_B",
                definition.Name + "/A_B"));

        EventProviderValidationResult result =
            EventProviderDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code == "GeneratedSymbolCollision");
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                "GeneratedLocalizationIdCollision");
    }

    [Fact]
    public void VersionQualifiesSymbolsForOneEventNameAcrossVersions() {
        EventProviderDefinition definition = CreateDefinition();
        ProviderEventDefinition versionOne =
            ProviderEventDefinition.FromType<ScanCompletedPayload>(
                "ScanCompleted",
                1000,
                "Operational",
                version: 1);
        versionOne.Messages["en-US"] =
            "Version one scan of {ComputerName} found {FindingCount} issues.";
        definition.AddEvent(versionOne);

        EventProviderValidationResult validation =
            EventProviderDefinitionValidator.Validate(
                definition);
        string manifest =
            EventProviderManifestGenerator.Generate(
                definition,
                "provider.resources.dll");

        Assert.True(validation.IsValid);
        Assert.Contains(
            "symbol=\"SCANCOMPLETED_V0\"",
            manifest);
        Assert.Contains(
            "symbol=\"SCANCOMPLETED_V1\"",
            manifest);
    }

    [Fact]
    public void RefusesToClaimANonDedicatedProviderRoot() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "unrelated.txt"),
            "do not modify");
        try {
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    EventProviderManagedDirectorySecurity
                        .EnsureManagedRoot(
                            root,
                            TimeSpan.FromSeconds(10)));

            Assert.Contains(
                "unrelated content",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RefusesToClaimAnUnverifiedGuidNamedDirectory() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        string unrelated = Path.Combine(
            root,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(
            Path.Combine(unrelated, "unrelated.txt"),
            "do not modify");
        try {
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    EventProviderManagedDirectorySecurity
                        .EnsureManagedRoot(
                            root,
                            TimeSpan.FromSeconds(10)));

            Assert.Contains(
                "unrelated content",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.False(
                File.Exists(
                    Path.Combine(
                        root,
                        ".eventviewerx-provider-root")));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SerializesLifecycleOperationsByProviderIdentity() {
        Guid providerId = Guid.NewGuid();
        using EventProviderLifecycleLock first =
            EventProviderLifecycleLock.Acquire(
                providerId,
                TimeSpan.FromSeconds(1));

        Exception? failure = null;
        var contender = new Thread(() => {
            try {
                using EventProviderLifecycleLock second =
                    EventProviderLifecycleLock.Acquire(
                        providerId,
                        TimeSpan.FromMilliseconds(100));
            } catch (Exception exception) {
                failure = exception;
            }
        });
        contender.Start();
        Assert.True(contender.Join(TimeSpan.FromSeconds(2)));
        Assert.IsType<TimeoutException>(failure);
    }

    [Fact]
    public void SerializesLifecycleOperationsByProviderNameIgnoringCase() {
        string providerName =
            "EventViewerX.Tests." +
            Guid.NewGuid().ToString("N");
        using EventProviderLifecycleLock first =
            EventProviderLifecycleLock.AcquireProviderName(
                providerName,
                TimeSpan.FromSeconds(1));

        Exception? failure = null;
        var contender = new Thread(() => {
            try {
                using EventProviderLifecycleLock second =
                    EventProviderLifecycleLock.AcquireProviderName(
                        providerName.ToUpperInvariant(),
                        TimeSpan.FromMilliseconds(100));
            } catch (Exception exception) {
                failure = exception;
            }
        });
        contender.Start();
        Assert.True(
            contender.Join(
                TimeSpan.FromSeconds(2)));
        Assert.IsType<TimeoutException>(failure);
    }

    [Fact]
    public void SerializesLifecycleOperationsByProviderRoot() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        using EventProviderLifecycleLock first =
            EventProviderLifecycleLock.AcquireProviderRoot(
                root,
                TimeSpan.FromSeconds(1));

        Exception? failure = null;
        var contender = new Thread(() => {
            try {
                using EventProviderLifecycleLock second =
                    EventProviderLifecycleLock.AcquireProviderRoot(
                        root + Path.DirectorySeparatorChar,
                        TimeSpan.FromMilliseconds(100));
            } catch (Exception exception) {
                failure = exception;
            }
        });
        contender.Start();
        Assert.True(
            contender.Join(
                TimeSpan.FromSeconds(2)));
        Assert.IsType<TimeoutException>(failure);
    }

    [Fact]
    public void ProducesIdenticalSignedPackagesForIdenticalInputs() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=EventViewerX Reproducibility Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate =
            request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1));
        try {
            EventProviderPackageBuildResult first =
                EventProviderPackageBuilder.Build(
                    CreateDefinition(),
                    Path.Combine(root, "first.evxprovider"),
                    new EventProviderPackageBuildOptions {
                        SigningCertificate = certificate
                    });
            EventProviderPackageBuildResult second =
                EventProviderPackageBuilder.Build(
                    CreateDefinition(),
                    Path.Combine(root, "second.evxprovider"),
                    new EventProviderPackageBuildOptions {
                        SigningCertificate = certificate
                    });

            Assert.Equal(
                first.PackageSha256,
                second.PackageSha256);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreflightIdentityIsBoundToTheExactVerifiedPackageBytes() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string candidatePath = Path.Combine(
            root,
            "candidate.evxprovider");
        string replacementPath = Path.Combine(
            root,
            "replacement.evxprovider");
        try {
            EventProviderPackageBuildResult candidate =
                EventProviderPackageBuilder.Build(
                    CreateDefinition(),
                    candidatePath);
            EventProviderDefinition replacementDefinition =
                CreateDefinition();
            replacementDefinition.PackageVersion =
                "1.0.1";
            EventProviderPackageBuilder.Build(
                replacementDefinition,
                replacementPath);

            using EventProviderPackage preflight =
                EventProviderPackageReader.Open(
                    candidatePath);
            Assert.Equal(
                candidate.PackageSha256,
                preflight.PackageSha256);

            File.Copy(
                replacementPath,
                candidatePath,
                overwrite: true);
            using EventProviderPackage replacement =
                EventProviderPackageReader.Open(
                    candidatePath);

            Assert.NotEqual(
                preflight.PackageSha256,
                replacement.PackageSha256);
            Assert.Throws<InvalidDataException>(() =>
                EventProviderPackageManager
                    .EnsureMatchesPreflight(
                        preflight,
                        preflight.PackageSha256,
                        replacement));
        } finally {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void RejectsAFileWhoseSignedHashWasTampered() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string packagePath = Path.Combine(
            root,
            "tampered.evxprovider");
        try {
            EventProviderPackageBuilder.Build(
                CreateDefinition(),
                packagePath);
            using (ZipArchive archive = ZipFile.Open(
                       packagePath,
                       ZipArchiveMode.Update)) {
                ZipArchiveEntry definition =
                    archive.GetEntry("provider.definition.json")!;
                using Stream stream = definition.Open();
                stream.SetLength(0);
                using var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(false),
                    1024,
                    leaveOpen: true);
                writer.Write("{}");
            }

            Assert.Throws<InvalidDataException>(() =>
                EventProviderPackageReader.Open(packagePath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedExtractionPreservesCallerOwnedEmptyDirectory() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string packagePath = Path.Combine(
            root,
            "inconsistent.evxprovider");
        string destinationPath = Path.Combine(
            root,
            "caller-owned");
        Directory.CreateDirectory(destinationPath);
        try {
            EventProviderPackageBuilder.Build(
                CreateDefinition(),
                packagePath);
            EventProviderDefinition changedDefinition =
                CreateDefinition();
            changedDefinition.PackageVersion = "1.0.1";
            byte[] definitionBytes =
                Encoding.UTF8.GetBytes(
                    EventProviderDefinitionJson.Serialize(
                        changedDefinition));

            using (ZipArchive archive = ZipFile.Open(
                       packagePath,
                       ZipArchiveMode.Update)) {
                ZipArchiveEntry definitionEntry =
                    archive.GetEntry(
                        EventProviderPackageLayout
                            .DefinitionFileName)!;
                ReplaceEntry(
                    definitionEntry,
                    definitionBytes);
                ZipArchiveEntry manifestEntry =
                    archive.GetEntry(
                        EventProviderPackageLayout
                            .PackageManifestFileName)!;
                EventProviderPackageManifest manifest;
                using (Stream stream = manifestEntry.Open()) {
                    manifest =
                        JsonSerializer.Deserialize<
                            EventProviderPackageManifest>(
                            stream,
                            EventProviderDefinitionJson
                                .SerializerOptions)!;
                }
                manifest.Files[
                    EventProviderPackageLayout
                        .DefinitionFileName] =
                    EventProviderHash.BytesSha256(
                        definitionBytes);
                ReplaceEntry(
                    manifestEntry,
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(
                            manifest,
                            EventProviderDefinitionJson
                                .SerializerOptions)));
            }

            Assert.Throws<InvalidDataException>(() =>
                EventProviderPackageReader.Extract(
                    packagePath,
                    destinationPath));
            Assert.True(
                Directory.Exists(destinationPath));
            Assert.Empty(
                Directory.EnumerateFileSystemEntries(
                    destinationPath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExtractionWriteDoesNotOverwriteACompetingPackageFile() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string competingPath = Path.Combine(
            root,
            EventProviderPackageLayout.DefinitionFileName);
        try {
            File.WriteAllText(
                competingPath,
                "competing-content",
                new UTF8Encoding(false));
            var writtenPaths = new List<string>();

            Assert.Throws<IOException>(() =>
                EventProviderPackageReader.WriteNewFile(
                    competingPath,
                    Encoding.UTF8.GetBytes("replacement"),
                    writtenPaths));
            Assert.Equal(
                "competing-content",
                File.ReadAllText(competingPath));
            Assert.Empty(writtenPaths);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TemporaryCleanupFailuresDoNotReplaceTheBuildOutcome() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string temporaryPackage = Path.Combine(
            root,
            "provider.tmp");
        string buildRoot = Path.Combine(
            root,
            "build");
        Directory.CreateDirectory(buildRoot);
        File.WriteAllText(temporaryPackage, "temporary");
        try {
            Exception? failure = Record.Exception(() =>
                EventProviderPackageBuilder
                    .CleanupTemporaryArtifacts(
                        temporaryPackage,
                        buildRoot,
                        static _ => throw new IOException(
                            "File is in use."),
                        static (_, _) => throw new IOException(
                            "Directory is in use.")));

            Assert.Null(failure);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void ReplaceEntry(
        ZipArchiveEntry entry,
        byte[] contents) {

        using Stream stream = entry.Open();
        stream.SetLength(0);
        stream.Write(
            contents,
            0,
            contents.Length);
    }

    internal static EventProviderDefinition CreateDefinition() {
        EventProviderDefinition definition =
            EventProviderDefinition.Create(
                "Evotec-EventViewerX-PackageTest",
                Guid.Parse("520ecea3-f786-459c-8b02-2a288cbef31c"),
                "1.0.0");
        definition.DisplayNames["en-US"] =
            "EventViewerX Package Test";
        definition.AddChannel(
            EventProviderChannelDefinition.Operational(
                "Operational",
                "Evotec-EventViewerX-PackageTest/Operational"));
        ProviderEventDefinition eventDefinition =
            ProviderEventDefinition.FromType<ScanCompletedPayload>(
                "ScanCompleted",
                1000,
                "Operational");
        eventDefinition.Messages["en-US"] =
            "Scan of {ComputerName} found {FindingCount} issues.";
        definition.AddEvent(eventDefinition);
        return definition;
    }

    private sealed class ScanCompletedPayload {
        [EventProviderPayloadField(0)]
        public string ComputerName { get; set; } = string.Empty;

        [EventProviderPayloadField(1)]
        public uint FindingCount { get; set; }
    }

    private sealed class InvalidArrayPayload {
        [EventProviderPayloadField(0)]
        public uint[] Values { get; set; } = Array.Empty<uint>();
    }
}
