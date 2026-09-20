namespace EventViewerX.Evtx;

/// <summary>Tracks XML element depth without allocating a second parsed document.</summary>
internal sealed class EvtxDumpXmlDepthTracker {
    private ElementFrame[] _elements = new ElementFrame[16];
    private ScanState _state;
    private int _depth;
    private char _quote;
    private char _lastTagCharacter;
    private bool _closingTag;
    private int _closingSequenceLength;
    private int _declarationBracketDepth;
    private ulong _tagHash1;
    private ulong _tagHash2;
    private int _tagNameLength;
    private bool _tagNameComplete;

    /// <summary>
    /// Gets whether a declaration at the start of the next line is in XML text rather than
    /// inside CDATA, a comment, a processing instruction, a declaration, or a tag.
    /// </summary>
    internal bool CanResynchronizeAtDocumentBoundary => _state == ScanState.Text;

    internal bool Process(string value) {
        bool completed = false;
        for (int index = 0; index < value.Length; index++) {
            char character = value[index];
            switch (_state) {
                case ScanState.Text:
                    if (completed) {
                        if (!char.IsWhiteSpace(character)) {
                            throw InvalidXml();
                        }
                        continue;
                    }
                    if (character != '<') {
                        continue;
                    }
                    if (StartsWith(value, index, "<!--")) {
                        _state = ScanState.Comment;
                        _closingSequenceLength = 0;
                        index += 3;
                    } else if (StartsWith(value, index, "<![CDATA[")) {
                        _state = ScanState.CData;
                        _closingSequenceLength = 0;
                        index += 8;
                    } else if (StartsWith(value, index, "<?")) {
                        _state = ScanState.ProcessingInstruction;
                        _closingSequenceLength = 0;
                        index++;
                    } else if (StartsWith(value, index, "</")) {
                        BeginTag(closing: true);
                        index++;
                    } else if (StartsWith(value, index, "<!")) {
                        _state = ScanState.Declaration;
                        _declarationBracketDepth = 0;
                        _quote = '\0';
                        index++;
                    } else {
                        BeginTag(closing: false);
                    }
                    break;
                case ScanState.Tag:
                    if (!_tagNameComplete && character is not ('>' or '/') && !char.IsWhiteSpace(character)) {
                        AddTagNameCharacter(character);
                        _lastTagCharacter = character;
                        break;
                    }
                    _tagNameComplete = true;
                    if (_quote != '\0') {
                        if (character == _quote) {
                            _quote = '\0';
                        }
                    } else if (character is '\'' or '"') {
                        _quote = character;
                    } else if (character == '>') {
                        var frame = new ElementFrame(
                            new TagFingerprint(_tagHash1, _tagHash2, _tagNameLength));
                        if (_closingTag) {
                            if (_depth == 0 || !_elements[_depth - 1].Name.Equals(frame.Name)) {
                                throw InvalidXml();
                            }
                            _depth--;
                            completed = _depth == 0;
                        } else if (_lastTagCharacter == '/') {
                            completed = _depth == 0;
                        } else {
                            Push(frame);
                        }
                        _state = ScanState.Text;
                    } else if (!char.IsWhiteSpace(character)) {
                        _lastTagCharacter = character;
                    }
                    break;
                case ScanState.Comment:
                    if (character == '-') {
                        _closingSequenceLength = Math.Min(2, _closingSequenceLength + 1);
                    } else if (character == '>' && _closingSequenceLength == 2) {
                        _state = ScanState.Text;
                        _closingSequenceLength = 0;
                    } else {
                        _closingSequenceLength = 0;
                    }
                    break;
                case ScanState.CData:
                    if (character == ']') {
                        _closingSequenceLength = Math.Min(2, _closingSequenceLength + 1);
                    } else if (character == '>' && _closingSequenceLength == 2) {
                        _state = ScanState.Text;
                        _closingSequenceLength = 0;
                    } else {
                        _closingSequenceLength = 0;
                    }
                    break;
                case ScanState.ProcessingInstruction:
                    if (character == '>') {
                        if (_closingSequenceLength == 1) {
                            _state = ScanState.Text;
                        }
                        _closingSequenceLength = 0;
                    } else {
                        _closingSequenceLength = character == '?' ? 1 : 0;
                    }
                    break;
                case ScanState.Declaration:
                    if (_quote != '\0') {
                        if (character == _quote) {
                            _quote = '\0';
                        }
                    } else if (character is '\'' or '"') {
                        _quote = character;
                    } else if (character == '[') {
                        _declarationBracketDepth++;
                    } else if (character == ']') {
                        _declarationBracketDepth = Math.Max(0, _declarationBracketDepth - 1);
                    } else if (character == '>' && _declarationBracketDepth == 0) {
                        _state = ScanState.Text;
                    }
                    break;
            }
        }
        return completed;
    }

    internal void Reset() {
        Array.Clear(_elements, 0, _depth);
        _state = ScanState.Text;
        _depth = 0;
        _quote = '\0';
        _lastTagCharacter = '\0';
        _closingTag = false;
        _closingSequenceLength = 0;
        _declarationBracketDepth = 0;
        ResetTagName();
    }

    private void BeginTag(bool closing) {
        _state = ScanState.Tag;
        _closingTag = closing;
        _lastTagCharacter = '\0';
        _quote = '\0';
        ResetTagName();
    }

    private void AddTagNameCharacter(char character) {
        const ulong offset1 = 14695981039346656037UL;
        const ulong prime1 = 1099511628211UL;
        const ulong offset2 = 7809847782465536322UL;
        const ulong prime2 = 14029467366897019727UL;
        if (_tagNameLength == 0) {
            _tagHash1 = offset1;
            _tagHash2 = offset2;
        }
        _tagHash1 = (_tagHash1 ^ character) * prime1;
        _tagHash2 = (_tagHash2 + character) * prime2;
        _tagNameLength++;
    }

    private void ResetTagName() {
        _tagHash1 = 0;
        _tagHash2 = 0;
        _tagNameLength = 0;
        _tagNameComplete = false;
    }

    private void Push(ElementFrame frame) {
        if (_depth == _elements.Length) {
            Array.Resize(ref _elements, checked(_elements.Length * 2));
        }
        _elements[_depth++] = frame;
    }

    private static bool StartsWith(string value, int first, string expected) =>
        first <= value.Length - expected.Length &&
        string.Compare(value, first, expected, 0, expected.Length, StringComparison.Ordinal) == 0;

    private static InvalidDataException InvalidXml() =>
        new("evtx_dump produced malformed Event XML framing.");

    private readonly struct ElementFrame {
        internal ElementFrame(TagFingerprint name) {
            Name = name;
        }

        internal TagFingerprint Name { get; }
    }

    private readonly struct TagFingerprint : IEquatable<TagFingerprint> {
        internal TagFingerprint(ulong hash1, ulong hash2, int length) {
            Hash1 = hash1;
            Hash2 = hash2;
            Length = length;
        }

        private ulong Hash1 { get; }

        private ulong Hash2 { get; }

        private int Length { get; }

        public bool Equals(TagFingerprint other) =>
            Hash1 == other.Hash1 && Hash2 == other.Hash2 && Length == other.Length;
    }

    private enum ScanState {
        Text,
        Tag,
        Comment,
        CData,
        ProcessingInstruction,
        Declaration
    }
}
