using System;
using System.Text;

namespace MainDen.Modules.Text
{
    public abstract class HexadecimalEncoding : Encoding
    {
        private static readonly object lSettings = new object();

        private class HexEncoding : HexadecimalEncoding
        {
            public override string EncodingName => "Hex";

            public override int GetByteCount(char[] chars, int index, int count)
            {
                if (chars is null)
                    throw new ArgumentNullException(nameof(chars));
                int length = chars.Length;
                if (index < 0 || length <= index && index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int end = index + count;
                if (count < 0 || length < end)
                    throw new ArgumentOutOfRangeException(nameof(count));
                int byteCount = 0;
                bool hasPair = false;
                for (int i = index; i < end; ++i)
                {
                    char c = chars[i];
                    if (IsHexChar(c))
                    {
                        if (hasPair)
                        {
                            ++byteCount;
                            hasPair = false;
                        }
                        else
                            hasPair = true;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (hasPair)
                        {
                            ++byteCount;
                            hasPair = false;
                        }
                    }
                    else
                        throw new EncoderFallbackException(@"Must contains only '\s0-9a-fA-F' symbols.");
                }
                if (hasPair)
                    ++byteCount;
                return byteCount;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
            {
                if (chars is null)
                    throw new ArgumentNullException(nameof(chars));
                int charLength = chars.Length;
                if (charIndex < 0 || charLength <= charIndex && charIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(charIndex));
                int charEnd = charIndex + charCount;
                if (charCount < 0 || charLength < charEnd)
                    throw new ArgumentOutOfRangeException(nameof(charCount));
                if (bytes is null)
                    throw new ArgumentNullException(nameof(bytes));
                int byteLength = bytes.Length;
                if (byteIndex < 0 || byteLength <= byteIndex && byteIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex));
                int byteCount = GetByteCount(chars, charIndex, charCount);
                int byteEnd = byteIndex + byteCount;
                if (byteCount < 0 || byteLength < byteEnd)
                    throw new ArgumentException("There is not enough byte capacity from byteIndex to the end of the array to accommodate the received bytes.");
                bool hasPair = false;
                byte b = 0;
                int bytesWrited = 0; // Can be removed by returning byteCount
                for (int i = charIndex; i < charEnd; ++i)
                {
                    char c = chars[i];
                    if (IsHexChar(c))
                    {
                        if (hasPair)
                        {
                            b = (byte)(0x10 * b + GetByteFromLowHexChar(c));
                            bytes[byteIndex++] = b;
                            ++bytesWrited;
                            b = 0;
                            hasPair = false;
                        }
                        else
                        {
                            b = GetByteFromLowHexChar(c);
                            hasPair = true;
                        }
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (hasPair)
                        {
                            bytes[byteIndex++] = b;
                            ++bytesWrited;
                            b = 0;
                            hasPair = false;
                        }
                    }
                    else
                        throw new EncoderFallbackException(@"Must contains only '\s0-9a-fA-F' symbols.");
                }
                if (hasPair)
                {
                    bytes[byteIndex++] = b;
                    ++bytesWrited;
                }
                return bytesWrited;
            }

            public override int GetCharCount(byte[] bytes, int index, int count)
            {
                if (bytes is null)
                    throw new ArgumentNullException(nameof(bytes));
                int length = bytes.Length;
                if (index < 0 || length <= index && index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int end = index + count;
                if (count < 0 || length < end)
                    throw new ArgumentOutOfRangeException(nameof(count));
                if (count == 0)
                    return 0;
                return count * 3 - 1;
            }

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            {
                if (bytes is null)
                    throw new ArgumentNullException(nameof(bytes));
                int byteLength = bytes.Length;
                if (byteIndex < 0 || byteLength <= byteIndex && byteIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex));
                int byteEnd = byteIndex + byteCount;
                if (byteCount < 0 || byteLength < byteEnd)
                    throw new ArgumentOutOfRangeException(nameof(byteCount));
                if (chars is null)
                    throw new ArgumentNullException(nameof(chars));
                int charLength = chars.Length;
                if (charIndex < 0 || charLength <= charIndex && charIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex));
                int charCount = GetCharCount(bytes, byteIndex, byteCount);
                int charEnd = charIndex + charCount;
                if (charCount < 0 || charLength < charEnd)
                    throw new ArgumentException("There is not enough byte capacity from charIndex to the end of the array to accommodate the received chars.");
                bool needSpace = false;
                int charsWrited = 0; // Can be removed by returning charCount
                for (int i = byteIndex; i < byteEnd; ++i)
                {
                    if (needSpace)
                    {
                        chars[charIndex++] = ' ';
                        ++charsWrited;
                    }
                    else
                        needSpace = true;
                    chars[charIndex++] = GetHighHexChar(bytes[i]);
                    ++charsWrited;
                    chars[charIndex++] = GetLowHexChar(bytes[i]);
                    ++charsWrited;
                }
                return charsWrited;
            }

            public override int GetMaxByteCount(int charCount)
            {
                if (charCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(charCount), "The value must not be negative.");
                return charCount / 2 + charCount % 2;
            }

            public override int GetMaxCharCount(int byteCount)
            {
                if (byteCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(byteCount), "The value must not be negative.");
                if (byteCount == 0)
                    return 0;
                return byteCount * 3 - 1;
            }
        }

        private class HASCIIEncoding : HexadecimalEncoding
        {
            private enum Helper : int
            {
                Start = 0,
                Escaping = 1,
                Hex = 2,
                Other = 3,
            }

            private const byte ESCByteBegin = (byte)ESCCharBegin;

            private const byte ESCByteEnd = (byte)ESCCharEnd;

            private const char ESCCharBegin = '{';

            private const char ESCCharEnd = '}';

            public override string EncodingName => "Hex" + ESCCharBegin + "US-ASCII" + ESCCharEnd;

            public override int GetByteCount(char[] chars, int index, int count)
            {
                if (chars is null)
                    throw new ArgumentNullException(nameof(chars));
                int length = chars.Length;
                if (index < 0 || length <= index && index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int end = index + count;
                if (count < 0 || length < end)
                    throw new ArgumentOutOfRangeException(nameof(count));
                int byteCount = 0;
                bool hasPair = false;
                bool ascii = false;
                for (int i = index; i < end; ++i)
                {
                    char c = chars[i];
                    if (ascii)
                    {
                        if (c == ESCByteEnd)
                            ascii = false;
                        else
                            byteCount += 1;
                    }
                    else if (c == ESCByteBegin)
                    {
                        if (hasPair)
                        {
                            ++byteCount;
                            hasPair = false;
                        }
                        ascii = true;
                    }
                    else if (c == ESCByteEnd)
                    {
                        if (hasPair)
                        {
                            ++byteCount;
                            hasPair = false;
                        }
                        byteCount += 1;
                    }
                    else if (IsHexChar(c))
                    {
                        if (hasPair)
                        {
                            ++byteCount;
                            hasPair = false;
                        }
                        else
                            hasPair = true;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (hasPair)
                        {
                            ++byteCount;
                            hasPair = false;
                        }
                    }
                    else
                        throw new EncoderFallbackException($"Must contains only '\\s{ESCCharBegin}{ESCCharEnd}0-9a-fA-F' symbols.");
                }
                if (hasPair)
                    ++byteCount;
                return byteCount;
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
            {
                if (chars is null)
                    throw new ArgumentNullException(nameof(chars));
                int charLength = chars.Length;
                if (charIndex < 0 || charLength <= charIndex && charIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(charIndex));
                int charEnd = charIndex + charCount;
                if (charCount < 0 || charLength < charEnd)
                    throw new ArgumentOutOfRangeException(nameof(charCount));
                if (bytes is null)
                    throw new ArgumentNullException(nameof(bytes));
                int byteLength = bytes.Length;
                if (byteIndex < 0 || byteLength <= byteIndex && byteIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex));
                int byteCount = GetByteCount(chars, charIndex, charCount);
                int byteEnd = byteIndex + byteCount;
                if (byteCount < 0 || byteLength < byteEnd)
                    throw new ArgumentException("There is not enough byte capacity from byteIndex to the end of the array to accommodate the received bytes.");
                bool hasPair = false;
                bool ascii = false;
                byte b = 0;
                int bytesWrited = 0; // Can be removed by returning byteCount
                for (int i = charIndex; i < charEnd; ++i)
                {
                    char c = chars[i];
                    if (ascii)
                    {
                        if (c == ESCByteEnd)
                            ascii = false;
                        else
                        {
                            if (c > '\u00FF')
                                throw new EncoderFallbackException(@"Must contains only ASCII symbols.");
                            bytes[byteIndex++] = GetLowByte(c);
                            bytesWrited += 1;
                        }
                    }
                    else if (c == ESCByteBegin)
                    {
                        if (hasPair)
                        {
                            bytes[byteIndex++] = b;
                            ++bytesWrited;
                            hasPair = false;
                        }
                        ascii = true;
                    }
                    else if (c == ESCByteEnd)
                    {
                        if (hasPair)
                        {
                            bytes[byteIndex++] = b;
                            ++bytesWrited;
                            hasPair = false;
                        }
                        bytes[byteIndex++] = GetLowByte(c);
                        bytesWrited += 1;
                    }
                    else if (IsHexChar(c))
                    {
                        if (hasPair)
                        {
                            b = (byte)(0x10 * b + GetByteFromLowHexChar(c));
                            bytes[byteIndex++] = b;
                            ++bytesWrited;
                            hasPair = false;
                        }
                        else
                        {
                            b = GetByteFromLowHexChar(c);
                            hasPair = true;
                        }
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (hasPair)
                        {
                            bytes[byteIndex++] = b;
                            ++bytesWrited;
                            hasPair = false;
                        }
                    }
                    else
                        throw new EncoderFallbackException($"Must contains only '\\s{ESCCharBegin}{ESCCharEnd}0-9a-fA-F' symbols.");
                }
                if (hasPair)
                {
                    bytes[byteIndex++] = b;
                    ++bytesWrited;
                }
                return bytesWrited;
            }

            public override int GetCharCount(byte[] bytes, int index, int count)
            {
                if (bytes is null)
                    throw new ArgumentNullException(nameof(bytes));
                int length = bytes.Length;
                if (index < 0 || length <= index && index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int end = index + count;
                if (count < 0 || length < end)
                    throw new ArgumentOutOfRangeException(nameof(count));
                if (count == 0)
                    return 0;
                int charCount = 0;
                Helper helper = Helper.Start;
                for (int i = index; i < end; ++i)
                {
                    byte b = bytes[i];
                    if (b == ESCByteEnd)
                        switch (helper)
                        {
                            case Helper.Escaping:
                            case Helper.Hex:
                                charCount += 2;
                                helper = Helper.Other;
                                break;
                            case Helper.Start:
                            case Helper.Other:
                                charCount += 1;
                                helper = Helper.Other;
                                break;
                        }
                    else if (IsEscaping(b))
                        switch (helper)
                        {
                            case Helper.Start:
                            case Helper.Other:
                                charCount += 2;
                                helper = Helper.Escaping;
                                break;
                            case Helper.Escaping:
                                charCount += 1;
                                break;
                            case Helper.Hex:
                                charCount += 3;
                                helper = Helper.Escaping;
                                break;
                        }
                    else switch (helper)
                        {
                            case Helper.Start:
                                charCount += 2;
                                helper = Helper.Hex;
                                break;
                            case Helper.Escaping:
                                charCount += 4;
                                helper = Helper.Hex;
                                break;
                            case Helper.Other:
                                charCount += 3;
                                helper = Helper.Hex;
                                break;
                            case Helper.Hex:
                                charCount += 3;
                                break;
                        }
                }
                if (helper == Helper.Escaping)
                    charCount += 1;
                return charCount;
            }

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            {
                if (bytes is null)
                    throw new ArgumentNullException(nameof(bytes));
                int byteLength = bytes.Length;
                if (byteIndex < 0 || byteLength <= byteIndex && byteIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex));
                int byteEnd = byteIndex + byteCount;
                if (byteCount < 0 || byteLength < byteEnd)
                    throw new ArgumentOutOfRangeException(nameof(byteCount));
                if (chars is null)
                    throw new ArgumentNullException(nameof(chars));
                int charLength = chars.Length;
                if (charIndex < 0 || charLength <= charIndex && charIndex != 0)
                    throw new ArgumentOutOfRangeException(nameof(byteIndex));
                int charCount = GetCharCount(bytes, byteIndex, byteCount);
                int charEnd = charIndex + charCount;
                if (charCount < 0 || charLength < charEnd)
                    throw new ArgumentException("There is not enough byte capacity from charIndex to the end of the array to accommodate the received chars.");
                Helper helper = Helper.Start;
                int charsWrited = 0; // Can be removed by returning charCount
                for (int i = byteIndex; i < byteEnd; ++i)
                {
                    byte b = bytes[i];
                    if (b == ESCByteEnd)
                        switch (helper)
                        {
                            case Helper.Escaping:
                                chars[charIndex++] = ESCCharEnd;
                                chars[charIndex++] = ESCCharEnd;
                                charsWrited += 2;
                                helper = Helper.Other;
                                break;
                            case Helper.Hex:
                                chars[charIndex++] = ' ';
                                chars[charIndex++] = ESCCharEnd;
                                charsWrited += 2;
                                helper = Helper.Other;
                                break;
                            case Helper.Start:
                            case Helper.Other:
                                chars[charIndex++] = ESCCharEnd;
                                charsWrited += 1;
                                helper = Helper.Other;
                                break;
                        }
                    else if (IsEscaping(b))
                        switch (helper)
                        {
                            case Helper.Start:
                            case Helper.Other:
                                chars[charIndex++] = ESCCharBegin;
                                chars[charIndex++] = (char)b;
                                charsWrited += 2;
                                helper = Helper.Escaping;
                                break;
                            case Helper.Escaping:
                                chars[charIndex++] = (char)b;
                                charsWrited += 1;
                                break;
                            case Helper.Hex:
                                chars[charIndex++] = ' ';
                                chars[charIndex++] = ESCCharBegin;
                                chars[charIndex++] = (char)b;
                                charsWrited += 3;
                                helper = Helper.Escaping;
                                break;
                        }
                    else switch (helper)
                        {
                            case Helper.Start:
                                chars[charIndex++] = GetHighHexChar(b);
                                chars[charIndex++] = GetLowHexChar(b);
                                charsWrited += 2;
                                helper = Helper.Hex;
                                break;
                            case Helper.Escaping:
                                chars[charIndex++] = ESCCharEnd;
                                chars[charIndex++] = ' ';
                                chars[charIndex++] = GetHighHexChar(b);
                                chars[charIndex++] = GetLowHexChar(b);
                                charsWrited += 4;
                                helper = Helper.Hex;
                                break;
                            case Helper.Other:
                            case Helper.Hex:
                                chars[charIndex++] = ' ';
                                chars[charIndex++] = GetHighHexChar(b);
                                chars[charIndex++] = GetLowHexChar(b);
                                charsWrited += 3;
                                helper = Helper.Hex;
                                break;
                        }
                }
                if (helper == Helper.Escaping)
                {
                    chars[charIndex++] = ESCCharEnd;
                    charsWrited += 1;
                }
                return charsWrited;
            }

            public override int GetMaxByteCount(int charCount)
            {
                if (charCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(charCount), "The value must not be negative.");
                return charCount;
            }

            public override int GetMaxCharCount(int byteCount)
            {
                if (byteCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(byteCount), "The value must not be negative.");
                if (byteCount == 0)
                    return 0;
                return byteCount / 2 * 7 + byteCount % 2 * 4 - 1;
            }

            private static bool IsEscaping(byte b)
            {
                return 0x20 <= b && b < 0x7F;
            }
        }

        private static HexadecimalEncoding hex;

        private static HexadecimalEncoding hASCII;

        public static HexadecimalEncoding Hex
        {
            get
            {
                lock (lSettings)
                    return hex ?? (hex = new HexEncoding());
            }
        }

        public static HexadecimalEncoding HASCII
        {
            get
            {
                lock (lSettings)
                    return hASCII ?? (hASCII = new HASCIIEncoding());
            }
        }

        public static bool IsHexChar(char c)
        {
            return '0' <= c && c <= '9' || 'a' <= c && c <= 'f' || 'A' <= c && c <= 'F';
        }

        public static char GetHighHexChar(byte b)
        {
            b = (byte)(b >> 0x4);
            if (b > 0x9)
                return (char)('A' - 0xA + b);
            else
                return (char)('0' + b);
        }

        public static char GetLowHexChar(byte b)
        {
            b = (byte)(b & 0xF);
            if (b > 0x9)
                return (char)('A' - 0xA + b);
            else
                return (char)('0' + b);
        }

        public static byte GetByteFromHighHexChar(char c)
        {
            if ('0' <= c && c <= '9')
                return (byte)(0x10 * (c - '0'));
            if ('a' <= c && c <= 'f')
                return (byte)(0x10 * (c - 'a' + 0xa));
            if ('A' <= c && c <= 'F')
                return (byte)(0x10 * (c - 'A' + 0xA));
            throw new ArgumentOutOfRangeException(nameof(c), "The value can only be '0-9a-fA-F' symbols.");
        }

        public static byte GetByteFromLowHexChar(char c)
        {
            if ('0' <= c && c <= '9')
                return (byte)(c - '0');
            if ('a' <= c && c <= 'f')
                return (byte)(c - 'a' + 0xa);
            if ('A' <= c && c <= 'F')
                return (byte)(c - 'A' + 0xA);
            throw new ArgumentOutOfRangeException(nameof(c), "The value can only be '0-9a-fA-F' symbols.");
        }

        public static byte GetHighByte(char c)
        {
            return (byte)(c >> 8);
        }

        public static byte GetLowByte(char c)
        {
            return (byte)(c & 0xFF);
        }
    }
}
