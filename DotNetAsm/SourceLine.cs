//-----------------------------------------------------------------------------
// Copyright (c) 2017 informedcitizenry <informedcitizenry@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to 
// deal in the Software without restriction, including without limitation the 
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS 
// IN THE SOFTWARE.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetAsm
{
    /// <summary>
    /// SourceLine class encapsulates a single line of assembly source.
    /// </summary>
    public class SourceLine : IEquatable<SourceLine>, ICloneable
    {
        #region Exception

        /// <summary>
        /// An exception class to handle strings not properly closed in quotation marks
        /// </summary>
        public class QuoteNotEnclosedException : Exception
        {
            public override string Message
            {
                get
                {
                    return ErrorStrings.QuoteStringNotEnclosed;
                }
            }
        }

        #endregion

        #region Members

        bool _doNotAssemble;

        bool _comment;

        #region Static Members

        static Regex _regThree;
        static Regex _regThreeAlt;
        static Regex _regTwo;
        static Regex _regOne;
        static Regex _regUnicode;

        #endregion

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new SourceLine object.
        /// </summary>
        /// <param name="filename">The original source filename.</param>
        /// <param name="linenumber">The original source line number.</param>
        /// <param name="source">The unprocessed source string.</param>
        public SourceLine(string filename, int linenumber, string source)
        {
            Assembly = new List<byte>();
            Filename = filename;
            LineNumber = linenumber;
            Scope =
            Label =
            Instruction =
            Operand =
            Disassembly = string.Empty;
            SourceString = source;
        }

        /// <summary>
        /// Constructs a new SourceLine object.
        /// </summary>
        public SourceLine() :
            this(string.Empty, 0, string.Empty)
        {

        }

        #region Static Constructors

        /// <summary>
        /// Initializes the DotNetAsm.SourceLine class.
        /// </summary>
        static SourceLine()
        {
            _regThree = new Regex(@"^([^\s]+)\s+(([^\s]+)\s+(.+))$", RegexOptions.Compiled);
            _regThreeAlt = new Regex(@"^([^\s]+)\s*(=)\s*(.+)$", RegexOptions.Compiled);
            _regTwo = new Regex(@"^([^\s]+)\s+(.+)$", RegexOptions.Compiled);
            _regOne = new Regex(@"^([^\s]+)$", RegexOptions.Compiled);
            _regUnicode = new Regex(@"(\\u[a-fA-F0-9]{4}|\\U[a-fA-F0-9]{8})",
                                                                        RegexOptions.Compiled);
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Convert escaped code point expressions to actual Unicode strings.
        /// </summary>
        /// <param name="labelOperand">The label or operand string to convert</param>
        /// <returns>A string with unicode characters converted from code points</returns>
        string ConvertEscapedUnicode(string labelOperand)
        {
            return _regUnicode.Replace(labelOperand, match =>
            {
                int codepoint = int.Parse(match.Value.Substring(2), NumberStyles.HexNumber);
                return char.ConvertFromUtf32(codepoint);
            });
        }

        /// <summary>
        /// Parse the SourceLine's SourceString property into its component line,
        /// instruction and operand.
        /// </summary>
        /// <param name="checkInstruction">A callback to determine which part of the source
        /// is the instruction.</param>
        /// <exception cref="SourceLine.QuoteNotEnclosedException">SourceLine.QuoteNotEnclosedException</exception>
        public void Parse(Func<string, bool> checkInstruction)
        {
            bool double_enclosed = false;
            bool single_enclosed = false;

            int length = 0;
            for (; length < SourceString.Length; length++)
            {
                char c = SourceString[length];
                if (!single_enclosed && !double_enclosed && c == ';')
                    break;
                if (c == '"' && !single_enclosed)
                    double_enclosed = !double_enclosed;
                else if (c == '\'' && !double_enclosed)
                    single_enclosed = !single_enclosed;

            }

            string processed = SourceString.Substring(0, length).Trim();
            Match m = _regThree.Match(processed);
            if (string.IsNullOrEmpty(m.Value) == false)
            {
                if (checkInstruction(m.Groups[1].Value))
                {
                    Instruction = m.Groups[1].Value;
                    Operand = m.Groups[2].Value;
                }
                else
                {
                    Label = m.Groups[1].Value;
                    Instruction = m.Groups[3].Value;
                    Operand = m.Groups[4].Value;
                }
            }
            else
            {
                m = _regThreeAlt.Match(processed);
                if (string.IsNullOrEmpty(m.Value) == false)
                {
                    Label = m.Groups[1].Value;
                    Instruction = m.Groups[2].Value;
                    Operand = m.Groups[3].Value;
                }
                else
                {
                    m = _regTwo.Match(processed);
                    if (string.IsNullOrEmpty(m.Value) == false)
                    {
                        if (checkInstruction(m.Groups[2].Value))
                        {
                            Label = m.Groups[1].Value;
                            Instruction = m.Groups[2].Value;
                        }
                        else
                        {
                            Instruction = m.Groups[1].Value;
                            Operand = m.Groups[2].Value;
                        }
                    }
                    else
                    {
                        m = _regOne.Match(processed);
                        if (string.IsNullOrEmpty(m.Value) == false)
                        {
                            if (checkInstruction(m.Groups[1].Value))
                                Instruction = m.Groups[1].Value;
                            else
                                Label = m.Groups[1].Value;
                        }
                    }
                }
            }
            // both label and operand may contain escaped Unicode characters
            Label = ConvertEscapedUnicode(Label);
            Operand = ConvertEscapedUnicode(Operand);
        }

        /// <summary>
        /// A unique identifier combination of the source's filename and line number.
        /// </summary>
        /// <returns>The identifier string.</returns>
        public string SourceInfo()
        {
            string file = Filename;
            if (file.Length > 14)
                file = Filename.Substring(0, 14) + "...";
            return string.Format("{0, -17}({1})", file, LineNumber);
        }

        #endregion

        #region Override Methods

        public override string ToString()
        {
            if (DoNotAssemble)
                return string.Format("Do Not Assemble {0}", SourceString);
            return string.Format("Line {0} ${1:X4} L:{2} I:{3} O:{4}",
                                                        LineNumber
                                                      , PC
                                                      , Label
                                                      , Instruction
                                                      , Operand);
        }

        public override int GetHashCode()
        {
            return LineNumber.GetHashCode() + Filename.GetHashCode() + SourceString.GetHashCode();
        }

        #endregion

        #region IEquatable

        public bool Equals(SourceLine other)
        {
            return (other.LineNumber == this.LineNumber &&
                    other.Filename == this.Filename &&
                    other.SourceString == this.SourceString);
        }

        #endregion

        #region ICloneable

        public object Clone()
        {
            SourceLine clone = new SourceLine
            {
                LineNumber = this.LineNumber,
                Filename = this.Filename,
                Label = this.Label,
                Operand = this.Operand,
                Instruction = this.Instruction,
                SourceString = this.SourceString,
                Scope = this.Scope,
                PC = this.PC
            };
            return clone;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the SourceLine's unique id number.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the SourceLine's Line number in the original source file.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the Program Counter of the assembly at the SourceLine.
        /// </summary>
        public long PC { get; set; }

        /// <summary>
        /// Gets or sets the SourceLine's original source filename.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Gets or sets the SourceLine scope.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Gets or sets the individual assembled bytes.
        /// </summary>
        public List<byte> Assembly { get; set; }

        /// <summary>
        /// Gets or sets the original (unparsed) source string.
        /// </summary>
        public string SourceString { get; set; }

        /// <summary>
        /// Gets or sets the disassembled representation of the source.
        /// </summary>
        public string Disassembly { get; set; }

        /// <summary>
        /// Gets or sets the flag determining whether the SourceLine 
        /// is actually part of a comment block. Setting this flag 
        /// also sets the flag to determine whether the SourceLine 
        /// is to be assembled. 
        /// </summary>
        public bool IsComment
        {
            get { return _comment; }
            set
            {
                _comment = value;
                if (_comment)
                    _doNotAssemble = _comment;
            }
        }

        public bool DoNotAssemble
        {
            get { return _doNotAssemble; }
            set
            {
                if (!IsComment)
                    _doNotAssemble = value;
            }
        }

        /// <summary>
        /// The SourceLine's label/symbol. This can be determined using the Parse method.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// The SourceLine's instruction. This can be determined using the Parse method.
        /// </summary>
        public string Instruction { get; set; }

        /// <summary>
        /// The SourceLine's operand. This can be determined using the Parse method.
        /// </summary>
        public string Operand { get; set; }

        #endregion
    }
}
