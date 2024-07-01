namespace NAsciidoc.Parser
{
    public class Reader(IEnumerable<string> lines)
    {
        private readonly IList<string> _lines = new List<string>(lines);
        private int _lineOffset = 0;

        // human indexed
        public int LineNumber => _lineOffset + 1;

        public void Reset()
        {
            _lineOffset = 0;
        }

        public void Insert(IList<string> lines)
        {
            int offset = 0;
            foreach (var it in lines)
            {
                _lines.Insert(_lineOffset + offset++, it);
            }
        }

        public void Rewind()
        {
            if (_lineOffset > 0)
            {
                _lineOffset--;
            }
        }

        public string? NextLine()
        {
            if (_lineOffset >= _lines.Count)
            {
                return null;
            }

            var line = _lines[_lineOffset];
            _lineOffset++;
            return line;
        }

        public string? SkipCommentsAndEmptyLines()
        {
            while (_lineOffset < _lines.Count)
            {
                var line = _lines[_lineOffset];
                _lineOffset++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("////"))
                { // go to the end of the comment
                    for (int i = _lineOffset + 1; i < _lines.Count; i++)
                    {
                        if (_lines[i].StartsWith("////"))
                        {
                            _lineOffset = i + 1;
                            break;
                        }
                    }
                    continue;
                }
                if (line.StartsWith("//"))
                {
                    continue;
                }

                return line;
            }

            // no line
            return null;
        }

        public bool IsComment(string line)
        {
            return line.StartsWith("//") || line.StartsWith("////");
        }

        public void SetPreviousValue(string newValue)
        {
            _lines[_lineOffset - 1] = newValue;
        }
    }
}
