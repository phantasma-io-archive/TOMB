using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.Tomb.Compiler
{
    public enum TokenKind
    {
        Separator,
        Operator,
        Selector,
        Identifier,
        Type,
        String,
        Number,
        Bool,
        Address,
        Hash,
        Bytes,
        Method,
        Macro,
    }

    public struct LexerToken
    {
        public readonly int column;
        public readonly int line;
        public readonly string value;
        public readonly TokenKind kind;

        public LexerToken(int column, int line, string value)
        {
            this.column = column;
            this.line = line;
            this.value = value;

            if (value.StartsWith("0x"))
            {
                this.kind = TokenKind.Bytes;
            }
            else
            if (value.StartsWith("@"))
            {
                this.kind = TokenKind.Address;
                this.value = value.Substring(1);

                Address addr;

                if (this.value.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    addr = Address.Null;
                }
                else
                {
                    addr = Address.FromText(this.value);
                }

                this.value = "0x" + Base16.Encode(addr.ToByteArray());
            }
            else
            if (value.StartsWith("#"))
            {
                this.kind = TokenKind.Hash;
                this.value = value.Substring(1);

                Hash hash = Hash.Parse(this.value);

                this.value = "0x" + hash.ToString();
            }
            else
            if (value.StartsWith("&"))
            {
                this.kind = TokenKind.Method;
                this.value = value.Substring(1);
            }
            else
            if (value.StartsWith("$"))
            {
                this.kind = TokenKind.Macro;
                this.value = value.Substring(1);
            }
            else
            if (value == "true" || value == "false")
            {
                this.kind = TokenKind.Bool;
            }
            else
            if (value == ".")
            {
                this.kind = TokenKind.Selector;
            }
            else
            if (value == ":=")
            {
                this.kind = TokenKind.Operator;
            }
            else
            if (BigInteger.IsParsable(value))
            {
                this.kind = TokenKind.Number;
            }
            else
            {
                var first = value[0];

                switch (first)
                {
                    case '\"':
                        this.kind = TokenKind.String;
                        break;

                    default:
                        if (Lexer.IsSeparatorSymbol(first))
                        {
                            this.kind = TokenKind.Separator;
                        }
                        else
                        if (Lexer.IsOperatorSymbol(first))
                        {
                            this.kind = TokenKind.Operator;
                        }
                        else
                        {
                            this.kind = TokenKind.Identifier;

                            foreach (var varType in Lexer.VarTypeNames)
                            {
                                if (varType == this.value)
                                {
                                    this.kind = TokenKind.Type;
                                    break;
                                }
                            }
                        }
                        break;
                }
            }

        }

        public override string ToString()
        {
            return $"{kind} => {value}";
        }
    }

    public static class Lexer
    {
        public readonly static string[] VarTypeNames = Enum.GetNames(typeof(VarKind)).Cast<string>().Select(x => x.ToLower()).ToArray();

        internal static bool IsOperatorSymbol(char ch)
        {
            switch (ch)
            {
                case '=':
                case '+':
                case '-':
                case '*':
                case '/':
                case '%':
                case '<':
                case '>':
                case '!':
                case ':':
                    return true;

                default:
                    return false;
            }
        }

        internal static bool IsSeparatorSymbol(char ch)
        {
            switch (ch)
            {
                case ';':
                case ',':
                case '{':
                case '}':
                case '(':
                case ')':
                case '.':
                    return true;

                default:
                    return false;
            }
        }

        public enum CommentMode
        {
            None,
            Single,
            Multi
        }

        public static List<LexerToken> Process(string sourceCode)
        {
            var tokens = new List<LexerToken>();
            int i = 0;

            int tokenX = 0;
            int tokenY = 0;

            int col = 0;
            int line = 1;
            var sb = new StringBuilder();

            bool insideString = false;
            var insideComment = CommentMode.None;

            int lastType = -1;
            char lastChar = '\0';
            while (i < sourceCode.Length)
            {
                var ch = sourceCode[i];
                i++;
                col++;

                if (insideComment == CommentMode.Single)
                {
                    if (ch == '\n')
                    {
                        insideComment = CommentMode.None;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                if (insideComment == CommentMode.Multi)
                {
                    if (ch == '/' && lastChar == '*')
                    {
                        insideComment = CommentMode.None;
                    }
                    else
                    {
                        lastChar = ch;
                    }
                    continue;
                }

                if (ch == '/' && lastChar == ch)
                {
                    sb.Length--;
                    insideComment = CommentMode.Single;
                    continue;
                }

                if (ch == '*' && lastChar == '/')
                {
                    sb.Length--;
                    insideComment = CommentMode.Multi;
                    continue;
                }

                switch (ch)
                {
                    case '\t':
                    case ' ':
                        if (insideString)
                        {
                            sb.Append(ch);
                        }
                        else
                        {
                            lastType = -1;
                        }

                        break;

                    case '\r':
                        break;

                    case '\n':
                        col = 0;
                        line++;
                        break;

                    default:
                        int curType;

                        if (ch == '/')
                        {
                            curType =0;
                        }
                        
                        if (ch == '\"')
                        {
                            insideString = !insideString;
                            curType = 0;
                        }
                        else
                        if (insideString)
                        {
                            curType = 0;
                        }
                        else
                        if (IsSeparatorSymbol(ch))
                        {
                            curType = 2;
                        }
                        else
                        if (IsOperatorSymbol(ch))
                        {
                            curType = 1;
                        }
                        else
                        {
                            curType = 0;
                        }

                        if (sb.Length > 0 && curType != lastType)
                        {
                            var val = sb.ToString();
                            tokens.Add(new LexerToken(tokenX, tokenY, val));
                            sb.Clear();
                        }

                        if (sb.Length == 0)
                        {
                            tokenX = col;
                            tokenY = line;
                        }

                        sb.Append(ch);

                        if (curType == 2)
                        {
                            curType = -1;
                        }

                        lastType = curType;
                        break;
                }

                lastChar = ch;
            }

            if (sb.Length > 0)
            {
                var val = sb.ToString();
                tokens.Add(new LexerToken(tokenX, tokenY, val));
            }

            return tokens;
        }
    }
}
