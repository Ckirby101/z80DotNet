﻿//-----------------------------------------------------------------------------
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
using System.Linq;

namespace DotNetAsm
{
    /// <summary>
    /// A class that assembles miscellaneous directives, such as error and warn messages.
    /// </summary>
    public class MiscAssembler : AssemblerBase, ILineAssembler
    {
        #region Constructors

        /// <summary>
        /// Constructs a DotNetAsm.MiscAssembler class.
        /// </summary>
        /// <param name="controller">The DotNetAsm.IAssemblyController to associate</param>
        public MiscAssembler(IAssemblyController controller) :
            base(controller)
        {
            Reserved.DefineType("Directives",
                    "assert", ".eor", ".echo", ".target",
                    ".error", ".errorif",
                    ".warnif", ".warn"
                );
        }

        #endregion

        #region Methods

        /// <summary>
        /// Throw a conditional error or warning.
        /// </summary>
        /// <param name="line">The SourceLine with the operand condition.</param>
        void ThrowConditional(SourceLine line)
        {
            var csv = line.Operand.CommaSeparate();
            if (csv.Count < 2)
                Controller.Log.LogEntry(line, ErrorStrings.TooFewArguments, line.Instruction);
            else if (csv.Count > 2)
                Controller.Log.LogEntry(line, ErrorStrings.TooManyArguments, line.Instruction);
            else if (Controller.Evaluator.EvalCondition(csv.First()))
                Output(line, csv.Last());
        }

        /// <summary>
        /// Sets the byte value to XOR all values when outputted to assembly.
        /// Used by the .eor directive.
        /// </summary>
        /// <param name="line">The SourceLine.</param>
        void SetEor(SourceLine line)
        {
            if (string.IsNullOrEmpty(line.Operand))
            {
                Controller.Log.LogEntry(line, ErrorStrings.TooFewArguments, line.Instruction);
                return;
            }
            Int64 eor = Controller.Evaluator.Eval(line.Operand);

            if (eor < 0) eor += 256;
            if (eor > 255 || eor < 0)
            {
                Controller.Log.LogEntry(line, ErrorStrings.IllegalQuantity, eor);
                return;
            }

            byte eor_b = Convert.ToByte(eor);
            Controller.Output.Transforms.Push(delegate (byte b)
            {
                b ^= eor_b;
                return b;
            });
        }

        public void AssembleLine(SourceLine line)
        {
            string instruction = Controller.Options.CaseSensitive ? line.Instruction : line.Instruction.ToLower();
            switch (instruction)
            {
                case ".assert":
                    DoAssert(line);
                    break;
                case ".warnif":
                case ".errorif":
                    ThrowConditional(line);
                    break;
                case ".echo":
                case ".error":
                case ".warn":
                    Output(line, line.Operand);
                    break;
                case ".eor":
                    SetEor(line);
                    break;
                case ".target":
                    if (!line.Operand.EnclosedInQuotes())
                        Controller.Log.LogEntry(line, ErrorStrings.QuoteStringNotEnclosed);
                    else
                        Controller.Options.Architecture = line.Operand.Trim('"');
                    break;
                default:
                    Controller.Log.LogEntry(line, ErrorStrings.UnknownInstruction, line.Instruction);
                    break;
            }
        }

        void Output(SourceLine line, string operand)
        {
            if (!operand.EnclosedInQuotes())
            {
                operand = StringAssemblerBase.GetFormattedString(line.Operand, Controller.Evaluator);
                if (string.IsNullOrEmpty(operand))
                    Controller.Log.LogEntry(line, ErrorStrings.QuoteStringNotEnclosed);
            }
            else
            {
                operand = operand.Trim('"');
            }
            string type = line.Instruction.Substring(0, 5).ToLower();
            switch (type)
            {
                case ".echo":
                    Console.WriteLine(operand);
                    break;
                case ".asse":
                case ".erro":
                    Controller.Log.LogEntry(line, operand);
                    break;
                case ".warn":
                    Controller.Log.LogEntry(line, operand, Controller.Options.WarningsAsErrors);
                    break;
            }
        }

        void DoAssert(SourceLine line)
        {
            var parms = line.Operand.CommaSeparate();
            if (parms.Count == 0)
            {
                Controller.Log.LogEntry(line, ErrorStrings.TooFewArguments, line.Instruction);
            }
            else if (parms.Count > 2)
            {
                Controller.Log.LogEntry(line, ErrorStrings.TooManyArguments, line.Instruction);
            }
            else if (!Controller.Evaluator.EvalCondition(parms.First()))
            {
                if (parms.Count > 1)
                    Output(line, parms.Last());
                else
                    Controller.Log.LogEntry(line, ErrorStrings.AssertionFailure, line.Operand);
            }
        }

        public int GetInstructionSize(SourceLine line)
        {
            return 0;
        }

        public bool AssemblesInstruction(string instruction)
        {
            return Reserved.IsReserved(instruction);
        }

        #endregion
    }
}