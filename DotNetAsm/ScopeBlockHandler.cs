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

<<<<<<< Updated upstream
using System;
=======
>>>>>>> Stashed changes
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotNetAsm
{
    /// <summary>
    /// Handles all scoped blocks.
    /// </summary>
    public class ScopeBlockHandler : AssemblerBase, IBlockHandler
    {
        #region Members

        int _anon;
        readonly Stack<string> _scope;
        readonly List<SourceLine> _processedLines;

        #endregion

        public ScopeBlockHandler(IAssemblyController controller)
            : base(controller)
        {
            Reserved.DefineType("Scoped", ConstStrings.OPEN_SCOPE, ConstStrings.CLOSE_SCOPE);
            _scope = new Stack<string>();
            _processedLines = new List<SourceLine>();
            _anon = 0;
        }

<<<<<<< Updated upstream
        public IEnumerable<SourceLine> GetProcessedLines()
        {
            return _processedLines;
        }

        public bool IsProcessing()
        {
            return _scope.Count > 0;
        }
=======
        public IEnumerable<SourceLine> GetProcessedLines() => _processedLines;

        public bool IsProcessing() => _scope.Count > 0;
>>>>>>> Stashed changes

        public void Process(SourceLine line)
        {
            var rev = _scope.Reverse();
            StringBuilder scopeBuilder = new StringBuilder(line.Scope);
            foreach (string s in rev)
                scopeBuilder.AppendFormat("{0}.", s);

            line.Scope = scopeBuilder.ToString();

            if (line.Instruction.Equals(ConstStrings.OPEN_SCOPE, Controller.Options.StringComparison))
            {
                if (string.IsNullOrEmpty(line.Label))
                    _scope.Push((_anon++).ToString());
                else
                    _scope.Push(line.Label);
            }
            else if (line.Instruction.Equals(ConstStrings.CLOSE_SCOPE, Controller.Options.StringComparison))
            {
                if (_scope.Count == 0)
                {
                    Controller.Log.LogEntry(line, ErrorStrings.ClosureDoesNotCloseBlock, line.Instruction);
                    return;
                }
                _scope.Pop();
            }

            var clone = line.Clone() as SourceLine;
            if (Reserved.IsReserved(clone.Instruction))
            {
                clone.SourceString = clone.Label;
                clone.Instruction = string.Empty;
            }
            _processedLines.Add(clone);
        }

<<<<<<< Updated upstream
        public bool Processes(string token)
        {
            return Reserved.IsReserved(token);
        }
=======
        public bool Processes(string token) => Reserved.IsReserved(token);
>>>>>>> Stashed changes

        public void Reset()
        {
            _scope.Clear();
            _processedLines.Clear();
        }
    }
}
