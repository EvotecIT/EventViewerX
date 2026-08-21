using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EventViewerX.Tests;

public class TestRuleSourceGuards
{
    private static readonly IReadOnlyList<string> KnownFieldKeys = new[]
    {
        "SubjectUserName",
        "SubjectDomainName",
        "SubjectLogonId",
        "TargetUserName",
        "TargetDomainName",
        "TargetSid",
        "WorkstationName",
        "IpAddress",
        "IpPort",
        "LogonType",
        "Status",
        "SubStatus",
        "FailureReason",
        "AuthenticationPackageName",
        "LogonProcessName",
        "LmPackageName",
        "KeyLength",
        "ProcessId",
        "ProcessName",
        "TransmittedServices",
        "TicketOptions",
        "TicketEncryptionType",
        "PreAuthType",
        "ClientIP",
        "HWAddress",
        "MacAddress",
        "PrivilegeList"
    };

    [Fact]
    public void RuleSources_DoNotUseDirectEventObjectDataIndexer()
    {
        Regex directIndexerPattern = new(@"Event\s*\.\s*Data\s*\[", RegexOptions.Compiled);
        IReadOnlyList<string> offenders = FindRuleSourceOffenders(directIndexerPattern);

        Assert.True(
            offenders.Count == 0,
            "Direct Event.Data[...] usage is forbidden in rules. Use TryGetDataValue/GetDataValueOrEmpty. Offenders:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void RuleSources_DoNotUseDirectDataTryGetValueCalls()
    {
        Regex directTryGetValuePattern = new(@"\b(?:Event|eventObject)\s*\.\s*Data\s*\.\s*TryGetValue\s*\(", RegexOptions.Compiled);
        IReadOnlyList<string> offenders = FindRuleSourceOffenders(directTryGetValuePattern);

        Assert.True(
            offenders.Count == 0,
            "Direct eventObject.Data.TryGetValue(...) is forbidden in rules. Use TryGetDataValue instead. Offenders:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void RuleSources_DoNotUseKnownFieldStringLiteralsInAccessors()
    {
        string knownKeysPattern = string.Join("|", KnownFieldKeys.Select(Regex.Escape));
        Regex knownFieldLiteralPattern = new(
            $@"\b(?:GetValueFromDataDictionary|GetDataValueOrEmpty|TryGetDataValue|TryGetDataEnum)\s*\([^)]*""(?:{knownKeysPattern})""",
            RegexOptions.Compiled);

        IReadOnlyList<string> offenders = FindRuleSourceOffenders(knownFieldLiteralPattern);

        Assert.True(
            offenders.Count == 0,
            "Known event fields should use KnownEventField enum accessors instead of string literals. Offenders:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void RuleSources_DoNotUseDirectSubjectTargetKnownFieldPairCalls()
    {
        Regex directSubjectTargetPairPattern = new(
            @"\bGetValueFromDataDictionary\s*\(\s*KnownEventField\.SubjectUserName\s*,\s*KnownEventField\.SubjectDomainName|\bGetValueFromDataDictionary\s*\(\s*KnownEventField\.TargetUserName\s*,\s*KnownEventField\.TargetDomainName",
            RegexOptions.Compiled);

        IReadOnlyList<string> offenders = FindRuleSourceOffenders(directSubjectTargetPairPattern);

        Assert.True(
            offenders.Count == 0,
            "Use GetSubjectAccountOrEmpty()/GetTargetAccountOrEmpty() instead of direct Subject/Target known-field pair calls. Offenders:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    private static IReadOnlyList<string> FindRuleSourceOffenders(Regex pattern)
    {
        string rulesDirectory = ResolveRulesDirectory();
        string repositoryRoot = Directory.GetParent(Directory.GetParent(rulesDirectory)!.FullName)!.FullName;

        var offenders = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(rulesDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                    trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!pattern.IsMatch(line))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(repositoryRoot, filePath);
                offenders.Add($"{relativePath}:{i + 1}: {trimmed}");
            }
        }

        return offenders;
    }

    private static string ResolveRulesDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "Sources", "EventViewerX", "Rules");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate Sources/EventViewerX/Rules from test base directory.");
    }
}
