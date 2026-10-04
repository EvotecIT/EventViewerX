using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Xml;
using evtx;
using evtx.Tags;

namespace EventViewerX.Evtx;

/// <summary>Serializes parser nodes without the dependency's lossy string formatting.</summary>
internal sealed class EvtxTemplateXmlRenderer {
    private const int MaximumDepth = 128;
    private const int MaximumTokens = 100_000;
    private const int MaximumXmlCharacters = 4 * 1024 * 1024;
    private static readonly UnicodeEncoding Unicode = new(false, false, true);
    private static readonly ConcurrentDictionary<Type, TemplateAccess> Accessors = new();
    private static readonly FieldInfo ChunkField = typeof(OpenStartElementTag).GetField("_chunk", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidDataException("The pinned parser does not expose its node chunk context.");
    private Template _currentTemplate = null!;
    private readonly Dictionary<Template, bool> _safeTemplates = new();
    private int _safeTemplateChunk = -1;
    private ChunkInfo? _nestedChunk;
    private int _nestedChunkNumber = -1;
    private int _tokens;
    private StringBuilder _output = null!;

    internal string Render(EventRecord record) {
        IBinXml instance = record.Nodes.SingleOrDefault(static node =>
            node.TagType == TagBuilder.BinaryTag.TemplateInstance)
            ?? throw new InvalidDataException("The EVTX record does not contain a template instance.");
        TemplateAccess access = Accessors.GetOrAdd(instance.GetType(), static type => new TemplateAccess(type));
        var template = access.Template.GetValue(instance) as Template
            ?? throw new InvalidDataException("The parser's template property is unavailable.");
        var entries = access.Entries.GetValue(instance) as List<SubstitutionArrayEntry>
            ?? throw new InvalidDataException("The parser's substitution entries are unavailable.");
        _currentTemplate = template;
        if (_safeTemplateChunk != record.ChunkNumber) {
            _safeTemplates.Clear();
            _safeTemplateChunk = record.ChunkNumber;
        }
        if (!_safeTemplates.TryGetValue(template, out bool safeTemplate)) {
            safeTemplate = IsSafeTemplate(template.Nodes, 0);
            _safeTemplates.Add(template, safeTemplate);
        }
        if (safeTemplate && entries.All(static entry => IsSafeSubstitution(entry))) {
            // Plain ASCII cannot be changed by the dependency's control-character
            // cleanup or XML escaping. Keep that common path allocation-light.
            string plainXml = instance.AsXml(null!, record.RecordPosition);
            if (plainXml.Length > MaximumXmlCharacters) {
                throw new InvalidDataException("Rendered EVTX XML exceeds its character bound.");
            }
            return plainXml;
        }
        _tokens = 0;
        _output = new StringBuilder();
        using (XmlWriter writer = XmlWriter.Create(_output, new XmlWriterSettings {
            OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.Entitize,
            CheckCharacters = true, ConformanceLevel = ConformanceLevel.Fragment
        })) {
            RenderTemplate(instance, record.RecordPosition, record.ChunkNumber, writer,
                new Dictionary<string, string>(StringComparer.Ordinal), 0);
        }
        if (_output.Length > MaximumXmlCharacters) {
            throw new InvalidDataException("Rendered EVTX XML exceeds its character bound.");
        }
        return _output.ToString();
    }

    private void RenderTemplate(IBinXml instance, long position, int chunkNumber,
        XmlWriter writer, IReadOnlyDictionary<string, string> namespaces, int depth) {
        CountToken(depth);
        // TemplateInstance is internal in the pinned parser; its two public
        // properties expose the existing parsed template and raw substitutions.
        // Validate that shape explicitly rather than falling back to lossy XML.
        TemplateAccess access = Accessors.GetOrAdd(instance.GetType(), static type => new TemplateAccess(type));
        var template = access.Template.GetValue(instance) as Template
            ?? throw new InvalidDataException("The parser's template property is unavailable.");
        var entries = access.Entries.GetValue(instance) as List<SubstitutionArrayEntry>
            ?? throw new InvalidDataException("The parser's substitution entries are unavailable.");
        Dictionary<int, SubstitutionArrayEntry> substitutions = entries.ToDictionary(static entry => entry.Position);
        foreach (IBinXml node in template.Nodes) {
            if (node is OpenStartElementTag element) {
                RenderElement(element, substitutions, position, chunkNumber, writer, namespaces, depth + 1);
            } else if (node.TagType != TagBuilder.BinaryTag.StartOfBXmlStream &&
                       node.TagType != TagBuilder.BinaryTag.EndOfBXmlStream) {
                throw new InvalidDataException($"Unsupported template root token {node.TagType}.");
            }
        }
    }

    private void RenderElement(OpenStartElementTag element, IReadOnlyDictionary<int, SubstitutionArrayEntry> substitutions,
        long position, int chunkNumber, XmlWriter writer, IReadOnlyDictionary<string, string> inherited, int depth) {
        CountToken(depth);
        var attributes = new List<KeyValuePair<string, string>>();
        IReadOnlyDictionary<string, string> namespaces = inherited;
        foreach (evtx.Tags.Attribute attribute in element.Attributes) {
            string? value = ReadText(attribute.AttributeInfo, substitutions, optionalAttribute: true);
            if (value == null) {
                continue;
            }
            attributes.Add(new KeyValuePair<string, string>(attribute.Name, value));
            if (attribute.Name == "xmlns" || attribute.Name.StartsWith("xmlns:", StringComparison.Ordinal)) {
                var updated = namespaces.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
                updated[attribute.Name == "xmlns" ? string.Empty : attribute.Name.Substring(6)] = value;
                namespaces = updated;
            }
        }
        SplitName(element.Name.Value, out string prefix, out string localName);
        writer.WriteStartElement(prefix, localName, ResolveNamespace(prefix, namespaces));
        foreach (KeyValuePair<string, string> attribute in attributes) {
            if (attribute.Key == "xmlns") {
                writer.WriteAttributeString("xmlns", attribute.Value);
            } else if (attribute.Key.StartsWith("xmlns:", StringComparison.Ordinal)) {
                writer.WriteAttributeString("xmlns", attribute.Key.Substring(6), null, attribute.Value);
            } else {
                SplitName(attribute.Key, out string attributePrefix, out string attributeName);
                writer.WriteAttributeString(attributePrefix, attributeName,
                    attributePrefix.Length == 0 ? string.Empty : ResolveNamespace(attributePrefix, namespaces), attribute.Value);
            }
        }
        foreach (IBinXml node in element.Nodes) {
            CountToken(depth);
            if (node is OpenStartElementTag child) {
                RenderElement(child, substitutions, position, chunkNumber, writer, namespaces, depth + 1);
            } else if ((node is OptionalSubstitution optional && optional.ValueType == TagBuilder.ValueType.BinXmlType) ||
                       (node is NormalSubstitution normal && normal.ValueType == TagBuilder.ValueType.BinXmlType)) {
                int slot = node is OptionalSubstitution optionalNode ? optionalNode.SubstitutionId
                    : ((NormalSubstitution)node).SubstitutionId;
                SubstitutionArrayEntry entry = substitutions[slot];
                using var reader = new BinaryReader(new MemoryStream(entry.DataBytes), Encoding.UTF8);
                while (reader.BaseStream.Position < reader.BaseStream.Length) {
                    IBinXml nested = TagBuilder.BuildTag(position, reader, GetNestedChunk(chunkNumber));
                    if (nested.TagType == TagBuilder.BinaryTag.EndOfBXmlStream) {
                        break;
                    }
                    if (nested.TagType == TagBuilder.BinaryTag.TemplateInstance) {
                        RenderTemplate(nested, position, chunkNumber, writer, namespaces, depth + 1);
                    } else if (nested.TagType != TagBuilder.BinaryTag.StartOfBXmlStream) {
                        throw new InvalidDataException($"Unsupported nested template token {nested.TagType}.");
                    }
                }
            } else if (node is Value || node is NormalSubstitution || node is OptionalSubstitution) {
                writer.WriteString(ReadText(node, substitutions, optionalAttribute: false));
            } else if (node.TagType != TagBuilder.BinaryTag.CloseStartElementTag &&
                       node.TagType != TagBuilder.BinaryTag.CloseEmptyElementTag &&
                       node.TagType != TagBuilder.BinaryTag.EndElementTag) {
                // Character/entity references and processing nodes are parsed as
                // XML fragments; DTDs and external resolution stay disabled.
                using XmlReader fragment = XmlReader.Create(new StringReader(node.AsXml(null!, position)),
                    new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment,
                        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                fragment.Read();
                while (!fragment.EOF) {
                    writer.WriteNode(fragment, false);
                }
            }
            if (_output.Length > MaximumXmlCharacters) {
                throw new InvalidDataException("Rendered EVTX XML exceeds its character bound.");
            }
        }
        writer.WriteEndElement();
    }

    private static string? ReadText(IBinXml node, IReadOnlyDictionary<int, SubstitutionArrayEntry> substitutions,
        bool optionalAttribute) {
        if (node is Value value) {
            return value.ValueData;
        }
        int slot = node is NormalSubstitution normal ? normal.SubstitutionId
            : node is OptionalSubstitution optional ? optional.SubstitutionId
            : throw new InvalidDataException($"Unsupported value token {node.TagType}.");
        SubstitutionArrayEntry entry = substitutions[slot];
        if (optionalAttribute && node is OptionalSubstitution && entry.ValType == TagBuilder.ValueType.NullType) {
            return null;
        }
        if (entry.ValType == TagBuilder.ValueType.StringType) {
            return Unicode.GetString(entry.DataBytes).TrimEnd('\0');
        }
        if (entry.ValType == TagBuilder.ValueType.AnsiStringType) {
            return CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetString(entry.DataBytes).TrimEnd('\0');
        }
        return entry.GetDataAsString();
    }

    private static bool IsSafeTemplate(IEnumerable<IBinXml> nodes, int depth) {
        if (depth > MaximumDepth) {
            return false;
        }
        foreach (IBinXml node in nodes) {
            if (node is OpenStartElementTag element) {
                if (element.Attributes.Any(static attribute => attribute.AttributeInfo is Value value && !IsSafeAscii(value.ValueData)) ||
                    !IsSafeTemplate(element.Nodes, depth + 1)) {
                    return false;
                }
            } else if (node is Value value) {
                if (!IsSafeAscii(value.ValueData)) {
                    return false;
                }
            } else if (node is NormalSubstitution || node is OptionalSubstitution) {
                // The record's actual substitution type is checked separately;
                // optional BinXML slots commonly contain a null entry.
            } else if (node.TagType != TagBuilder.BinaryTag.StartOfBXmlStream &&
                       node.TagType != TagBuilder.BinaryTag.EndOfBXmlStream &&
                       node.TagType != TagBuilder.BinaryTag.CloseStartElementTag &&
                       node.TagType != TagBuilder.BinaryTag.CloseEmptyElementTag &&
                       node.TagType != TagBuilder.BinaryTag.EndElementTag) {
                return false;
            }
        }
        return true;
    }

    private static bool IsSafeSubstitution(SubstitutionArrayEntry entry) {
        if (entry.ValType == TagBuilder.ValueType.BinXmlType) {
            return false;
        }
        bool unicode = entry.ValType == TagBuilder.ValueType.StringType || entry.ValType == TagBuilder.ValueType.ArrayUnicodeString;
        bool ansi = entry.ValType == TagBuilder.ValueType.AnsiStringType || entry.ValType == TagBuilder.ValueType.ArrayAsciiString;
        if (!unicode && !ansi) {
            return true;
        }
        int width = unicode ? 2 : 1;
        byte[] bytes = entry.DataBytes;
        if (bytes.Length % width != 0) {
            return false;
        }
        int end = bytes.Length;
        while (end >= width && (unicode ? BitConverter.ToUInt16(bytes, end - width) : bytes[end - width]) == 0) {
            end -= width;
        }
        for (int index = 0; index < end; index += width) {
            int character = unicode ? BitConverter.ToUInt16(bytes, index) : bytes[index];
            if (!IsSafeAsciiCharacter(character)) {
                return false;
            }
        }
        return true;
    }

    private static bool IsSafeAscii(string text) {
        foreach (char character in text) {
            if (!IsSafeAsciiCharacter(character)) {
                return false;
            }
        }
        return true;
    }

    private static bool IsSafeAsciiCharacter(int character) =>
        character >= 0x20 && character < 0x7F && character != '&' && character != '<' && character != '>' &&
        character != '"' && character != '\'';

    private ChunkInfo GetNestedChunk(int chunkNumber) {
        if (_nestedChunkNumber == chunkNumber && _nestedChunk != null) {
            return _nestedChunk;
        }
        OpenStartElementTag root = _currentTemplate.Nodes.OfType<OpenStartElementTag>().FirstOrDefault()
            ?? throw new InvalidDataException("The template does not contain a root element.");
        // The approved parser keeps the context of public nested nodes private.
        // Reuse that exact chunk; reparsing it would duplicate all record work.
        _nestedChunk = ChunkField.GetValue(root) as ChunkInfo
            ?? throw new InvalidDataException("The parser's node chunk context is unavailable.");
        _nestedChunkNumber = chunkNumber;
        return _nestedChunk;
    }

    private void CountToken(int depth) {
        if (++_tokens > MaximumTokens || depth > MaximumDepth) {
            throw new InvalidDataException("The EVTX template exceeds its token or depth bound.");
        }
    }

    private static void SplitName(string name, out string prefix, out string localName) {
        int colon = name.IndexOf(':');
        prefix = colon < 0 ? string.Empty : name.Substring(0, colon);
        localName = colon < 0 ? name : name.Substring(colon + 1);
    }

    private static string ResolveNamespace(string prefix, IReadOnlyDictionary<string, string> namespaces) =>
        prefix == "xml" ? "http://www.w3.org/XML/1998/namespace"
        : namespaces.TryGetValue(prefix, out string? value) ? value
        : prefix.Length == 0 ? string.Empty
        : throw new InvalidDataException($"Undeclared XML namespace prefix '{prefix}'.");

    private sealed class TemplateAccess {
        internal TemplateAccess(Type type) {
            Template = type.GetProperty("Template") ?? throw new InvalidDataException("The parser does not expose its template.");
            Entries = type.GetProperty("SubstitutionEntries") ?? throw new InvalidDataException("The parser does not expose raw substitutions.");
        }

        internal PropertyInfo Template { get; }
        internal PropertyInfo Entries { get; }
    }
}
