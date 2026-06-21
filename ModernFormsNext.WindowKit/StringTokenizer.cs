using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static System.Char;

namespace ModernFormsNext.WindowKit.Utilities
{
    /// <summary>
    /// Tokenizes a separated numeric or string value list while validating complete consumption.
    /// </summary>
    /// <remarks>
    /// Use this helper when parsing compact value syntax where tokens are separated by commas
    /// or by a culture-sensitive separator. Dispose the tokenizer after reading the expected
    /// values; disposal throws when unread input remains.
    /// </remarks>
#if !BUILDTASK
    public
#endif
    record struct StringTokenizer : IDisposable
    {
        private const char DefaultSeparatorChar = ',';

        private readonly string _s;
        private readonly int _length;
        private readonly char _separator;
        private readonly string? _exceptionMessage;
        private readonly IFormatProvider _formatProvider;
        private int _index;
        private int _tokenIndex;
        private int _tokenLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringTokenizer"/> struct using a culture-aware separator.
        /// </summary>
        /// <param name="s">The string to tokenize.</param>
        /// <param name="formatProvider">The format provider used for numeric parsing and separator selection.</param>
        /// <param name="exceptionMessage">The optional message used for format exceptions.</param>
        public StringTokenizer(string s, IFormatProvider formatProvider, string? exceptionMessage = null)
            : this(s, GetSeparatorFromFormatProvider(formatProvider), exceptionMessage)
        {
            _formatProvider = formatProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringTokenizer"/> struct.
        /// </summary>
        /// <param name="s">The string to tokenize.</param>
        /// <param name="separator">The token separator character.</param>
        /// <param name="exceptionMessage">The optional message used for format exceptions.</param>
        public StringTokenizer(string s, char separator = DefaultSeparatorChar, string? exceptionMessage = null)
        {
            _s = s ?? throw new ArgumentNullException(nameof(s));
            _length = s?.Length ?? 0;
            _separator = separator;
            _exceptionMessage = exceptionMessage;
            _formatProvider = CultureInfo.InvariantCulture;
            _index = 0;
            _tokenIndex = -1;
            _tokenLength = 0;

            while (_index < _length && IsWhiteSpace(_s, _index))
            {
                _index++;
            }
        }

        /// <summary>
        /// Gets the last token read by the tokenizer.
        /// </summary>
        public string? CurrentToken => _tokenIndex < 0 ? null : _s.Substring(_tokenIndex, _tokenLength);

        /// <summary>
        /// Validates that all input was consumed.
        /// </summary>
        /// <exception cref="FormatException">Thrown when unread input remains.</exception>
        public void Dispose()
        {
            if (_index != _length)
            {
                throw GetFormatException();
            }
        }

        /// <summary>
        /// Attempts to read the next token as a 32-bit integer.
        /// </summary>
        /// <param name="result">Receives the parsed integer when the method succeeds.</param>
        /// <param name="separator">An optional separator override for this read.</param>
        /// <returns><see langword="true"/> when an integer was read; otherwise, <see langword="false"/>.</returns>
        public bool TryReadInt32(out Int32 result, char? separator = null)
        {
            if (TryReadString(out var stringResult, separator) &&
                int.TryParse(stringResult, NumberStyles.Integer, _formatProvider, out result))
            {
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Reads the next token as a 32-bit integer.
        /// </summary>
        /// <param name="separator">An optional separator override for this read.</param>
        /// <returns>The parsed integer.</returns>
        /// <exception cref="FormatException">Thrown when the next token is missing or not an integer.</exception>
        public int ReadInt32(char? separator = null)
        {
            if (!TryReadInt32(out var result, separator))
            {
                throw GetFormatException();
            }

            return result;
        }

        /// <summary>
        /// Attempts to read the next token as a double-precision floating-point value.
        /// </summary>
        /// <param name="result">Receives the parsed value when the method succeeds.</param>
        /// <param name="separator">An optional separator override for this read.</param>
        /// <returns><see langword="true"/> when a value was read; otherwise, <see langword="false"/>.</returns>
        public bool TryReadDouble(out double result, char? separator = null)
        {
            if (TryReadString(out var stringResult, separator) &&
                double.TryParse(stringResult, NumberStyles.Float, _formatProvider, out result))
            {
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Reads the next token as a double-precision floating-point value.
        /// </summary>
        /// <param name="separator">An optional separator override for this read.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">Thrown when the next token is missing or not a double.</exception>
        public double ReadDouble(char? separator = null)
        {
            if (!TryReadDouble(out var result, separator))
            {
                throw GetFormatException();
            }

            return result;
        }

        /// <summary>
        /// Attempts to read the next token as a string.
        /// </summary>
        /// <param name="result">Receives the token text when the method succeeds.</param>
        /// <param name="separator">An optional separator override for this read.</param>
        /// <returns><see langword="true"/> when a token was read; otherwise, <see langword="false"/>.</returns>
        public bool TryReadString([MaybeNullWhen(false)] out string result, char? separator = null)
        {
            var success = TryReadToken(separator ?? _separator);
            result = CurrentToken;
            return success;
        }

        /// <summary>
        /// Reads the next token as a string.
        /// </summary>
        /// <param name="separator">An optional separator override for this read.</param>
        /// <returns>The token text.</returns>
        /// <exception cref="FormatException">Thrown when the next token is missing.</exception>
        public string ReadString(char? separator = null)
        {
            if (!TryReadString(out var result, separator))
            {
                throw GetFormatException();
            }

            return result;
        }

        private bool TryReadToken(char separator)
        {
            _tokenIndex = -1;

            if (_index >= _length)
            {
                return false;
            }

            var c = _s[_index];

            var index = _index;
            var length = 0;

            while (_index < _length)
            {
                c = _s[_index];

                if (IsWhiteSpace(c) || c == separator)
                {
                    break;
                }

                _index++;
                length++;
            }

            SkipToNextToken(separator);

            _tokenIndex = index;
            _tokenLength = length;

            if (_tokenLength < 1)
            {
                throw GetFormatException();
            }

            return true;
        }

        private void SkipToNextToken(char separator)
        {
            if (_index < _length)
            {
                var c = _s[_index];

                if (c != separator && !IsWhiteSpace(c))
                {
                    throw GetFormatException();
                }

                var length = 0;

                while (_index < _length)
                {
                    c = _s[_index];

                    if (c == separator)
                    {
                        length++;
                        _index++;

                        if (length > 1)
                        {
                            throw GetFormatException();
                        }
                    }
                    else
                    {
                        if (!IsWhiteSpace(c))
                        {
                            break;
                        }

                        _index++;
                    }
                }

                if (length > 0 && _index >= _length)
                {
                    throw GetFormatException();
                }
            }
        }

        private FormatException GetFormatException() =>
            _exceptionMessage != null ? new FormatException(_exceptionMessage) : new FormatException();

        private static char GetSeparatorFromFormatProvider(IFormatProvider provider)
        {
            var c = DefaultSeparatorChar;

            var formatInfo = NumberFormatInfo.GetInstance(provider);
            if (formatInfo.NumberDecimalSeparator.Length > 0 && c == formatInfo.NumberDecimalSeparator[0])
            {
                c = ';';
            }

            return c;
        }
    }
}
