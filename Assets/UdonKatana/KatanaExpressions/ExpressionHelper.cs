using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JLChnToZ.Katana.Expressions {
    internal static class ExpressionHelper {
        private static readonly Regex numberMatcher =
            new Regex("^(?:(?:0x([0-9A-F]+)|(0[0-7]+)|([+-]?[0-9]+)|([-+]?[0-9]*\\.?[0-9]+(?:E[-+]?[0-9]+)?)))(U|LU|UL|B|SB|BS|US|SU|S|M|F|D)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private enum TokenMode {
            Expression,
            ExpressionAfterClosingQuote,
            ExpressionAfterString,
            Escape,
            InString,
            InStringEscape,
            InStringEscapeOct,
            InStringEscapeHex,
        }

        public static void Serialize(Node node, StringBuilder sb) {
            if (node == null) return;
            var stack = new Stack<(bool, IEnumerator<Node>)>();
            stack.Push((false, Enumerable.Repeat(node, 1).GetEnumerator()));
            bool firstLine = true;
            while (stack.Count > 0) {
                var (isOpenQuote, currentDepth) = stack.Pop();
                if (!currentDepth.MoveNext()) {
                    if (IndentAndLinebreak(ref firstLine, stack.Count, sb))
                        sb.Append(')');
                    continue;
                }
                var currentNode = currentDepth.Current;
                stack.Push((true, currentDepth));
                if (isOpenQuote)
                    sb.Append(',');
                IndentAndLinebreak(ref firstLine, stack.Count, sb);
                bool hasSerializeName = SerializeName(currentNode.Tag, sb);
                if (currentNode.Count <= 0) {
                    if (!hasSerializeName)
                        sb.Append("nil");
                    continue;
                }
                if (hasSerializeName)
                    sb.Append(' ');
                sb.Append('(');
                stack.Push((false, currentNode.GetEnumerator()));
            }
        }

        private static bool SerializeName(object src, StringBuilder sb) {
            if (src == null)
                return false;
            switch (Convert.GetTypeCode(src)) {
                case TypeCode.Boolean:
                    sb.Append(Convert.ToBoolean(src) ?
                        "true" :
                        "false");
                    return true;
                case TypeCode.Single:
                    var floatValue = Convert.ToSingle(src);
                    if (double.IsInfinity(floatValue))
                        sb.Append(floatValue > 0 ?
                            "infinityF" :
                            "-infinityF");
                    else if (double.IsNaN(floatValue))
                        sb.Append("nanF");
                    else
                        sb.Append(floatValue);
                    return true;
                case TypeCode.Double:
                    var doubleValue = Convert.ToDouble(src);
                    if (double.IsInfinity(doubleValue))
                        sb.Append(doubleValue > 0 ?
                            "infinity" :
                            "-infinity");
                    else if (double.IsNaN(doubleValue))
                        sb.Append("nan");
                    else
                        sb.Append(doubleValue);
                    return true;
                case TypeCode.Byte:
                    sb.Append(src);
                    sb.Append("b");
                    return true;
                case TypeCode.SByte:
                    sb.Append(src);
                    sb.Append("sb");
                    return true;
                case TypeCode.Int16:
                    sb.Append(src);
                    sb.Append("s");
                    return true;
                case TypeCode.UInt16:
                    sb.Append(src);
                    sb.Append("us");
                    return true;
                case TypeCode.Int32:
                    sb.Append(src);
                    return true;
                case TypeCode.UInt32:
                    sb.Append(src);
                    sb.Append("u");
                    return true;
                case TypeCode.Int64:
                    sb.Append(src);
                    sb.Append("l");
                    return true;
                case TypeCode.UInt64:
                    sb.Append(src);
                    sb.Append("ul");
                    return true;
                case TypeCode.Decimal:
                    sb.Append(src);
                    sb.Append("m");
                    return true;
            }
            var srcStr = src.ToString();
            bool needWrap = false;
            switch (srcStr.ToLower()) {
                case "":
                case "nil":
                case "null":
                case "true":
                case "false":
                case "infinity":
                case "+infinity":
                case "-infinity":
                case "nan":
                    needWrap = true;
                    break;
                default:
                    if (numberMatcher.IsMatch(srcStr)) {
                        needWrap = true;
                        break;
                    }
                    foreach (var c in srcStr) {
                        switch (c) {
                            case '(':
                            case ')':
                            case ',':
                            case '\"':
                            case '\'':
                            case '\\':
                                needWrap = true;
                                break;
                            default:
                                if (CanIgnoreChar(c))
                                    needWrap = true;
                                break;
                        }
                        if (needWrap) break;
                    }
                    break;
            }
            if (needWrap)
                SerializeNameWithQuotes(srcStr, sb);
            else
                sb.Append(srcStr);
            return true;
        }

        private static void SerializeNameWithQuotes(string src, StringBuilder sb) {
            sb.Append('\"');
            foreach (var c in src)
                switch (c) {
                    case '\"':
                    case '\'':
                    case '\\':
                        sb.Append($"\\{c}");
                        break;
                    case '\0':
                        sb.Append("\\0");
                        break;
                    case '\a':
                        sb.Append("\\a");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\x1b':
                        sb.Append("\\e");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\v':
                        sb.Append("\\v");
                        break;
                    default:
                        if (!char.IsControl(c))
                            sb.Append(c);
                        else if (c < 256)
                            sb.Append($"\\x{(int)c:X2}");
                        else
                            sb.Append($"\\u{(int)c:X4}");
                        break;
                }
            sb.Append('\"');
        }

        private static bool IndentAndLinebreak(ref bool firstLine, int indent, StringBuilder sb) {
            if (firstLine)
                firstLine = false;
            else
                sb.AppendLine();
            if (indent > 0) {
                sb.Append(' ', (indent - 1) * 2);
                return true;
            }
            return false;
        }

        public static Node Deserizlize(string source) {
            var mode = TokenMode.Expression;
            var nodeStack = new Stack<Node>();
            var sb = new List<char>();
            char stringPairChar = '\0';
            bool hasQuote = false;
            bool hasAddNode = false;
            bool isComments = false;
            int byteLength = 0;
            int escapeCharCode = 0;
            int escapeReturnOffset = 0;
            Node current;
            Node result = null;
            for (int i = 0; i < source.Length; i++) {
                char c = source[i];
                if (isComments) {
                    if (c == '\n')
                        isComments = false;
                    continue;
                }
                switch (mode) {
                    case TokenMode.Expression:
                        switch (c) {
                            default:
                                sb.Add(c);
                                break;
                            case '(':
                                current = CreateNode(sb, hasQuote, false, i);
                                if (nodeStack.Count > 0)
                                    nodeStack.Peek().Add(current);
                                nodeStack.Push(current);
                                if (result == null)
                                    result = nodeStack.Peek();
                                hasQuote = false;
                                hasAddNode = false;
                                break;
                            case ',':
                                if (nodeStack.Count < 1)
                                    throw new SyntaxException($"Unexpected '{c}'", source, i);
                                nodeStack.Peek().Add(CreateNode(sb, hasQuote, false, i));
                                hasQuote = false;
                                hasAddNode = true;
                                break;
                            case ')':
                                if (nodeStack.Count < 1)
                                    throw new SyntaxException($"Unexpected '{c}'", source, i);
                                current = CreateNode(sb, hasQuote, !hasAddNode, i);
                                var parent = nodeStack.Pop();
                                if (current != null)
                                    parent.Add(current);
                                hasQuote = false;
                                hasAddNode = false;
                                mode = TokenMode.ExpressionAfterClosingQuote;
                                break;
                            case '\'':
                            case '\"':
                                if (sb.Count > 0)
                                    foreach (var chr in sb)
                                        if (!CanIgnoreChar(chr))
                                            throw new SyntaxException($"Unexpected '{c}'", source, i);
                                sb.Clear();
                                mode = TokenMode.InString;
                                stringPairChar = c;
                                hasQuote = true;
                                break;
                            case '\\':
                                mode = TokenMode.Escape;
                                break;
                            case ';':
                                isComments = true;
                                break;
                        }
                        break;
                    case TokenMode.ExpressionAfterClosingQuote:
                        if (CanIgnoreChar(c))
                            break;
                        switch (c) {
                            case ',':
                                mode = TokenMode.Expression;
                                hasQuote = false;
                                hasAddNode = true;
                                break;
                            case ')':
                                mode = TokenMode.Expression;
                                i--;
                                break;
                            case ';':
                                isComments = true;
                                break;
                            default:
                                throw new SyntaxException($"Unexpected '{c}'", source, i);
                        }
                        break;
                    case TokenMode.ExpressionAfterString:
                        switch (c) {
                            case '(':
                            case ',':
                            case ')':
                                mode = TokenMode.Expression;
                                i--;
                                break;
                            case ';':
                                isComments = true;
                                break;
                            default:
                                if (!CanIgnoreChar(c))
                                    throw new SyntaxException($"Unexpected '{c}'", source, i);
                                break;
                        }
                        break;
                    case TokenMode.Escape:
                        mode = TokenMode.Expression;
                        sb.Add(c);
                        break;
                    case TokenMode.InString:
                        switch (c) {
                            case '\'':
                            case '\"':
                                if (stringPairChar == c)
                                    mode = TokenMode.ExpressionAfterString;
                                else
                                    goto default;
                                break;
                            case '\\':
                                mode = TokenMode.InStringEscape;
                                break;
                            default:
                                sb.Add(c);
                                break;
                        }
                        break;
                    case TokenMode.InStringEscape:
                        mode = TokenMode.InString;
                        switch (c) {
                            default:
                                sb.Add(c);
                                break;
                            case 'a':
                                sb.Add('\a');
                                break;
                            case 'b':
                                sb.Add('\b');
                                break;
                            case 'e':
                                sb.Add('\x1b');
                                break;
                            case 'f':
                                sb.Add('\f');
                                break;
                            case 'n':
                                sb.Add('\n');
                                break;
                            case 'r':
                                sb.Add('\r');
                                break;
                            case 't':
                                sb.Add('\t');
                                break;
                            case 'v':
                                sb.Add('\v');
                                break;
                            case 'u':
                                mode = TokenMode.InStringEscapeHex;
                                escapeReturnOffset = i;
                                escapeCharCode = 0;
                                byteLength = 4;
                                break;
                            case 'U':
                                mode = TokenMode.InStringEscapeHex;
                                escapeReturnOffset = i;
                                escapeCharCode = 0;
                                byteLength = 8;
                                break;
                            case 'x':
                                mode = TokenMode.InStringEscapeHex;
                                escapeReturnOffset = i;
                                escapeCharCode = 0;
                                byteLength = 2;
                                break;
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                                mode = TokenMode.InStringEscapeOct;
                                escapeReturnOffset = i;
                                escapeCharCode = c - '0';
                                break;
                        }
                        break;
                    case TokenMode.InStringEscapeOct:
                        if (!AppendOct(c, ref escapeCharCode)) {
                            mode = TokenMode.InString;
                            if (source[escapeReturnOffset] == '0') {
                                i = escapeReturnOffset;
                                sb.Add('\0');
                            } else
                                i = escapeReturnOffset - 1;
                        } else if (i - escapeCharCode >= 2) {
                            mode = TokenMode.InString;
                            sb.Add((char)escapeCharCode);
                        }
                        break;
                    case TokenMode.InStringEscapeHex:
                        if (!AppendHex(c, ref escapeCharCode)) {
                            mode = TokenMode.InString;
                            i = escapeReturnOffset - 1;
                        } else if (i - escapeCharCode >= byteLength) {
                            mode = TokenMode.InString;
                            if (byteLength > 4)
                                sb.AddRange(char.ConvertFromUtf32(escapeCharCode));
                            else
                                sb.Add((char)escapeCharCode);
                        }
                        break;
                }
            }
            switch (mode) {
                case TokenMode.Expression:
                case TokenMode.ExpressionAfterClosingQuote:
                case TokenMode.ExpressionAfterString:
                    break;
                case TokenMode.Escape:
                    throw new SyntaxException("Escape does not resolve", source, source.Length);
                default:
                    throw new SyntaxException($"Missing '{stringPairChar}'", source, source.Length);
            }
            if (result == null)
                result = CreateNode(sb, hasQuote, true, source.Length);
            if (nodeStack.Count > 1)
                throw new SyntaxException("Missing ')'", source, source.Length);
            return result;
        }

        private static bool CanIgnoreChar(char c) =>
            char.IsWhiteSpace(c) || char.IsSeparator(c) || char.IsControl(c);

        private static Node CreateNode(List<char> sb, bool hasQuote, bool ignoreIfEmpty, int refPos) {
            try {
                if (sb.Count <= 0)
                    return ignoreIfEmpty ? null : new Node(null) { sourceOffset = refPos };
                var str = new string(sb.ToArray());
                if (hasQuote)
                    return new Node(str) { sourceOffset = refPos };
                if (string.IsNullOrWhiteSpace(str))
                    return ignoreIfEmpty ? null : new Node(null) { sourceOffset = refPos };
                str = str.Trim();
                object result = str;
                try {
                    switch (str.ToLower()) {
                        case "nil":
                        case "null":
                            result = null;
                            break;
                        case "infinity":
                        case "+infinity":
                            result = double.PositiveInfinity;
                            break;
                        case "-infinity":
                            result = double.NegativeInfinity;
                            break;
                        case "infinityf":
                        case "+infinityf":
                            result = float.PositiveInfinity;
                            break;
                        case "-infinityf":
                            result = float.NegativeInfinity;
                            break;
                        case "nan":
                            result = double.NaN;
                            break;
                        case "nanf":
                            result = float.NaN;
                            break;
                        case "true":
                            result = true;
                            break;
                        case "false":
                            result = false;
                            break;
                        default:
                            var matches = numberMatcher.Match(str);
                            if (!matches.Success)
                                break;
                            var groups = matches.Groups;
                            int baseValue = 10;
                            bool isFloat = false;
                            string rawValue = "";
                            if (groups[1].Length > 0) {
                                rawValue = groups[1].Value;
                                baseValue = 16;
                            } else if (matches.Groups[2].Length > 0) {
                                rawValue = groups[2].Value;
                                baseValue = 8;
                            } else if (matches.Groups[3].Length > 0) {
                                rawValue = groups[3].Value;
                            } else if (matches.Groups[4].Length > 0) {
                                rawValue = groups[4].Value;
                                isFloat = true;
                            }
                            switch (groups[5]?.Value?.ToLower()) {
                                case "b": result = isFloat ? (byte)Convert.ToDouble(rawValue) : Convert.ToByte(rawValue, baseValue); break;
                                case "sb": case "bs": result = isFloat ? (sbyte)Convert.ToDouble(rawValue) : Convert.ToSByte(rawValue, baseValue); break;
                                case "s": result = isFloat ? (ushort)Convert.ToDouble(rawValue) : Convert.ToUInt16(rawValue, baseValue); break;
                                case "us": case "su": result = isFloat ? (short)Convert.ToDouble(rawValue) : Convert.ToInt16(rawValue, baseValue); break;
                                default: result = isFloat ? (object)Convert.ToDouble(rawValue) : Convert.ToInt32(rawValue, baseValue); break;
                                case "u": result = isFloat ? (uint)Convert.ToDouble(rawValue) : Convert.ToUInt32(rawValue, baseValue); break;
                                case "l": result = isFloat ? (long)Convert.ToDouble(rawValue) : Convert.ToInt64(rawValue, baseValue); break;
                                case "ul": case "lu": result = isFloat ? (ulong)Convert.ToDouble(rawValue) : Convert.ToUInt64(rawValue, baseValue); break;
                                case "f": result = isFloat ? Convert.ToSingle(rawValue) : Convert.ToInt64(rawValue, baseValue); break;
                                case "d": result = isFloat ? Convert.ToDouble(rawValue) : Convert.ToInt64(rawValue, baseValue); break;
                                case "m": result = isFloat ? Convert.ToDecimal(rawValue) : Convert.ToInt64(rawValue, baseValue); break;
                            }
                            break;
                    }
                } catch { }
                return new Node(result) { sourceOffset = refPos };
            } finally {
                sb.Clear();
            }
        }

        private static bool AppendOct(char c, ref int num) {
            unchecked {
                switch (c) {
                    case '0': case '1': case '2': case '3':
                    case '4': case '5': case '6': case '7':
                        num = (num << 3) | (c - '0');
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        private static bool AppendHex(char c, ref int num) {
            unchecked {
                switch (c) {
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        num = (num << 4) | (c - '0');
                        break;
                    case 'A': case 'B': case 'C': case 'D': case 'E': case 'F':
                        num = (num << 4) | (c - 'A' + 10);
                        break;
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f':
                        num = (num << 4) | (c - 'a' + 10);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
    }

    public struct SourcePosition: IEquatable<SourcePosition>, IComparable<SourcePosition> {
        public readonly int line, column;

        public SourcePosition(int line, int column) {
            this.line = line;
            this.column = column;
        }

        public SourcePosition(string source, int offset) {
            column = offset;
            line = 1;
            if (!string.IsNullOrEmpty(source))
                for (int i = 0, l = Math.Min(offset, source.Length); i < l; i++)
                    switch (source[i]) {
                        case '\n':
                            if (i > 0 && source[i - 1] == '\r')
                                break;
                            goto case '\r';
                        case '\r':
                            column = offset - i;
                            line++;
                            break;
                    }
        }

        public int CompareTo(SourcePosition other) {
            if (line > other.line) return 1;
            if (line < other.line) return -1;
            if (column > other.column) return 1;
            if (column < other.column) return -1;
            return 0;
        }

        public bool Equals(SourcePosition other) => line == other.line && column == other.column;

        public override bool Equals(object obj) => obj is SourcePosition other && Equals(other);

        public override int GetHashCode() => unchecked(line << 16 ^ column);

        public override string ToString() => $"({line}:{column})";
  }

    public class SyntaxException: Exception {
        private int line, column = -1;

        public string SourceCode { get; private set; }

        public int Offset { get; private set; }

        public int Line {
            get {
                if (line <= 0) CountLine();
                return line;
            }
        }

        public int Column {
            get {
                if (column < 0) CountLine();
                return column;
            }
        }

        public override string Message => $"{base.Message} at ({Line}, {Column}).";

        public SyntaxException(string message, string source, int offset) :
            base(message) {
            SourceCode = source;
            Offset = offset;
        }

        private void CountLine() {
            var lineNumber = new SourcePosition(SourceCode, Offset);
            line = lineNumber.line;
            column = lineNumber.column;
        }
    }
}