﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNetAsm
{
    public sealed class MacroHandler : AssemblerBase, IBlockHandler
    {
        #region Members

        Dictionary<string, Macro> _macros;

        List<SourceLine> _expandedSource;

        Stack<List<SourceLine>> _macroDefinitions;

        Func<string, bool> _instructionFcn;

        Stack<SourceLine> _definitions;

        #endregion

        public MacroHandler(IAssemblyController controller, Func<string, bool> instructionFcn)
            : base(controller)
        {
            Reserved.DefineType("Directives", ".macro", ".endmacro", ".segment", ".endsegment");
            _macros = new Dictionary<string, Macro>(controller.Options.StringComparar);
            _expandedSource = new List<SourceLine>();
            _macroDefinitions = new Stack<List<SourceLine>>();
            _instructionFcn = instructionFcn;
            _definitions = new Stack<SourceLine>();
        }

        public bool Processes(string token)
        {
            return Reserved.IsReserved(token) || _macros.ContainsKey(token);
        }

        public void Process(SourceLine line)
        {
            string instruction = line.Instruction.ToLower();
            if (!Processes(line.Instruction))
            {
                if (instruction.Equals(_macros.Last().Key))
                    Controller.Log.LogEntry(line, ErrorStrings.RecursiveMacro, line.Instruction);
                _macroDefinitions.Peek().Add(line);
                return;
            }

            if (instruction.Equals(".macro") || instruction.Equals(".segment"))
            {
                _macroDefinitions.Push(new List<SourceLine>());
                string name;
                _definitions.Push(line);
                if (instruction.Equals(".segment"))
                {
                    if (!string.IsNullOrEmpty(line.Label))
                    {
                        Controller.Log.LogEntry(line, ErrorStrings.None);
                        return;
                    }
                    name = line.Operand;
                }
                else
                {
                    name = line.Label;
                }
                if (!Macro.IsValidMacroName(name) || _instructionFcn(name))
                {
                    Controller.Log.LogEntry(line, ErrorStrings.LabelNotValid, line.Label);
                    return;
                }
                if (_macros.ContainsKey("." + name))
                {
                    Controller.Log.LogEntry(line, ErrorStrings.MacroRedefinition, line.Label);
                    return;
                }
                _macros.Add("." + name, null);
            }
            else if (instruction.Equals(".endmacro") || instruction.Equals(".endsegment"))
            {
                string def = instruction.Replace("end",string.Empty);
                string name = "." + _definitions.Peek().Label;
                if (!_definitions.Peek().Instruction.Equals(def, Controller.Options.StringComparison))
                {
                    Controller.Log.LogEntry(line, ErrorStrings.ClosureDoesNotCloseMacro, line.Instruction);
                    return;
                }
                if (def.Equals(".segment"))
                {
                    name = _definitions.Peek().Operand;
                    if (!name.Equals(line.Operand, Controller.Options.StringComparison))
                    {
                        Controller.Log.LogEntry(line, ErrorStrings.None);
                        return;
                    }
                    name = "." + name;
                }
                _macros[name] = Macro.Create(_definitions.Pop(),
                                             line,
                                             _macroDefinitions.Pop(),
                                             Controller.Options.StringComparison,
                                             ConstStrings.OPEN_SCOPE,
                                             ConstStrings.CLOSE_SCOPE);
            }
            else
            {
                var macro = _macros[line.Instruction];
                if (macro == null)
                {
                    Controller.Log.LogEntry(line, ErrorStrings.MissingClosureMacro, line.Instruction);
                    return;
                }
                if (IsProcessing())
                {
                    _macroDefinitions.Peek().Remove(line);
                    _macroDefinitions.Peek().AddRange(macro.Expand(line).ToList());
                }
                else
                {
                    _expandedSource = macro.Expand(line).ToList();
                }
            }
        }

        public void Reset()
        {
            _macroDefinitions.Clear();
            _expandedSource.Clear();
            _definitions.Clear();
        }

        public bool IsProcessing()
        {
            return _definitions.Count > 0 || _macroDefinitions.Count > 0;
        }

        public IEnumerable<SourceLine> GetProcessedLines()
        {
            return _expandedSource;
        }
    }
}
