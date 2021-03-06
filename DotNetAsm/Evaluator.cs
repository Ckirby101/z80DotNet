﻿//-----------------------------------------------------------------------------
// Copyright (c) 2017, 2018 informedcitizenry <informedcitizenry@gmail.com>
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetAsm
{
    /// <summary>
    /// Exception class for evaluation expressions.
    /// </summary>
    public class ExpressionException : Exception
    {
        /// <summary>
        /// Gets or sets the expression string that raises the exception.
        /// </summary>
        public string ExpressionString { get; set; }

        /// <summary>
        /// Constructs an instance of the ExpressionException class.
        /// </summary>
        /// <param name="expression">The expression that raises the exception.</param>
        public ExpressionException(string expression)
        {
            ExpressionString = expression;
        }

        /// <summary>
        /// Overrides the Exception message.
        /// </summary>
        public override string Message
        {
            get
            {
                return "Unknown or invalid expression: " + ExpressionString;
            }
        }
    }

    /// <summary>
    /// Math expression evaluator class. Takes string input and parses and evaluates.
    /// </summary>
    public class Evaluator : IEvaluator
    {
        #region Constants

        /// <summary>
        /// Represents the string indicating the expression cannot be evaluated.
        /// </summary>
        public const string EVAL_FAIL = "?FAIL";

        #endregion

        #region Members

        #region Static Members

        static List<char> _operators = new List<char>
        {
            '∨', '∧', '<', '≤', '≠', '≡', '≥', '>', '≪', '≫',
            '-', '+', '^', '|', '&', '%', '/', '*', '↑'
        };

        #endregion

        Random _rng;
        Regex _regFcn, _regUnary, _regBinary;
        List<Tuple<Regex, string>> _replacements;
        List<Regex> _hexRegexes;
        Dictionary<string, double> _cache;
        readonly Dictionary<string, Tuple<Regex, Func<string, string>>> _symbolLookups;
        Dictionary<string, Tuple<Func<double[], double>, int>> _functions;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of the <see cref="T:DotNetAsm.Evaluator"/> class, used to evaluate 
        /// strings as mathematical expressions.
        /// </summary>
        /// <param name="hexPattern">The default hexadecimal pattern used to identify hexadecimal
        /// strings in an expression</param>
        public Evaluator(string hexPattern)
        {
            _symbolLookups = new Dictionary<string, Tuple<Regex, Func<string, string>>>();

            _regFcn = new Regex(@"(" + Patterns.SymbolBasic + @")(\(.+\))", RegexOptions.Compiled);
            _regUnary = new Regex(@"(?<![0-9.)<>])([!+\-~^&<>])(\(.+\)|[0-9.]+)", RegexOptions.Compiled);
            _regBinary = new Regex(@"(?<=^|[^01#.])%(([01]+)|([#.]+))", RegexOptions.Compiled);

            _cache = new Dictionary<string, double>();
            _hexRegexes = new List<Regex> { new Regex(hexPattern, RegexOptions.Compiled) };

            _rng = new Random();

            _replacements = new List<Tuple<Regex, string>>
            {
                new Tuple<Regex, string>(new Regex(@"\*\*", RegexOptions.Compiled), "↑")
            };
            _functions = new Dictionary<string, Tuple<Func<double[], double>, int>>
            {
                { "abs",    new Tuple<Func<double[], double>, int>(parms => Math.Abs(parms[0]),             1) },
                { "acos",   new Tuple<Func<double[], double>, int>(parms => Math.Acos(parms[0]),            1) },
                { "atan",   new Tuple<Func<double[], double>, int>(parms => Math.Atan(parms[0]),            1) },
                { "cbrt",   new Tuple<Func<double[], double>, int>(parms => Math.Pow(parms[0], 1.0 / 3.0),  1) },
                { "ceil",   new Tuple<Func<double[], double>, int>(parms => Math.Ceiling(parms[0]),         1) },
                { "cos",    new Tuple<Func<double[], double>, int>(parms => Math.Cos(parms[0]),             1) },
                { "cosh",   new Tuple<Func<double[], double>, int>(parms => Math.Cosh(parms[0]),            1) },
                { "deg",    new Tuple<Func<double[], double>, int>(parms => (parms[0] * 180 / Math.PI),     1) },
                { "exp",    new Tuple<Func<double[], double>, int>(parms => Math.Exp(parms[0]),             1) },
                { "floor",  new Tuple<Func<double[], double>, int>(parms => Math.Floor(parms[0]),           1) },
                { "frac",   new Tuple<Func<double[], double>, int>(parms => Math.Abs(parms[0] - Math.Abs(Math.Round(parms[0], 0))), 1) },
                { "hypot",  new Tuple<Func<double[], double>, int>(parms => Math.Sqrt(Math.Pow(parms[0], 2) + Math.Pow(parms[1], 2)), 2) },
                { "ln",     new Tuple<Func<double[], double>, int>(parms => Math.Log(parms[0]),             1) },
                { "log10",  new Tuple<Func<double[], double>, int>(parms => Math.Log10(parms[0]),           1) },
                { "pow",    new Tuple<Func<double[], double>, int>(parms => Math.Pow(parms[0], parms[1]),   2) },
                { "rad",    new Tuple<Func<double[], double>, int>(parms => (parms[0] * Math.PI / 180),     1) },
                { "random", new Tuple<Func<double[], double>, int>(parms => _rng.Next((int)parms[0], (int)parms[1]), 2) },
                { "sgn",    new Tuple<Func<double[], double>, int>(parms => Math.Sign(parms[0]),            1) },
                { "sin",    new Tuple<Func<double[], double>, int>(parms => Math.Sin(parms[0]),             1) },
                { "sinh",   new Tuple<Func<double[], double>, int>(parms => Math.Sinh(parms[0]),            1) },
                { "sqrt",   new Tuple<Func<double[], double>, int>(parms => Math.Sqrt(parms[0]),            1) },
                { "tan",    new Tuple<Func<double[], double>, int>(parms => Math.Tan(parms[0]),             1) },
                { "tanh",   new Tuple<Func<double[], double>, int>(parms => Math.Tanh(parms[0]),            1) },
                { "round",
                    new Tuple<Func<double[], double>, int>(delegate (double[] parms)
                     {
                         if (parms.Length == 2)
                             return Math.Round(parms[0], (int)parms[1]);
                         return Math.Round(parms[0]);
                     }, 2)
                }
            };
        }

        /// <summary>
        /// Constructs an instance of the <see cref="T:DotNetAsm.Evaluator"/> class, used to evaluate 
        /// strings as mathematical expressions.
        /// </summary>
        public Evaluator()
            : this(@"0x([a-fA-F0-9])+")
        {

        }

        #endregion

        #region Methods

        /// <summary>
        /// Evaluate function calls within the expression.
        /// </summary>
        /// <param name="expression">The expression to evaluate for function calls</param>
        /// <returns>The modified expression with return values substituted in
        /// for function calls</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        string EvalFunctions(string expression)
        {
            var m = _regFcn.Match(expression);
            while (string.IsNullOrEmpty(m.Value) == false)
            {
                var fcnName = m.Groups[1].Value;
                var call_list = m.Groups[2].Value.FirstParenEnclosure();
                var parens = EvalFunctions(call_list.Substring(1, call_list.Length - 2));

                var parms = parens.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                double result = 0;

                var fcn = _functions[fcnName];

                if (parms.Length > fcn.Item2)
                    throw new ExpressionException(expression);

                if (parms.Length == 2)
                    result = fcn.Item1(new double[] { EvalInternal(parms[0]), EvalInternal(parms[1]) });
                else
                    result = fcn.Item1(new double[] { EvalInternal(parms[0]) });

                expression = expression.Replace(fcnName + call_list, result.ToString());
                m = _regFcn.Match(expression);
            }
            return expression;
        }

        /// <summary>
        /// Evaluate the expression string for unary operations.
        /// </summary>
        /// <param name="expression">The expression to evaluate</param>
        /// <returns>The modified expression string with unary operations converted
        /// to binary operations</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        string EvalUnaries(string expression)
        {
            expression = Regex.Replace(expression, @"\s+(?=[!+\-~^&<>])", string.Empty);
            expression = _regUnary.Replace(expression, delegate (Match m)
            {
                // <value =  value        % 256
                // >value = (value/256)   % 256
                // ^value = (value/65536) % 256
                var value = m.Groups[2].Value.FirstParenEnclosure();
                var post = string.Empty;

                if (value != m.Groups[2].Value)
                {
                    int start = value.Length;
                    int end = m.Groups[2].Value.Length - start;
                    post = string.Format("{0}{1}", m.Groups[2].Value.Substring(start, 1),
                        EvalUnaries(m.Groups[2].Value.Substring(start + 1, end - 1)));
                }

                switch (m.Groups[1].Value)
                {
                    case "^":
                        return string.Format("((({0})/65536)%256){1}", value, post);
                    case ">":
                        return string.Format("((({0})/256)%256){1}", value, post);
                    case "&":
                        return string.Format("(({0})%65536){1}", value, post);
                    case "<":
                        return string.Format("(({0})%256){1}", value, post);
                    case "-":
                        return string.Format("(0-{0}){1}", value, post);
                    case "!":
                        return string.Format("{0}{1}", Eval(value) == 0 ? "1" : "0", post);
                    case "+":
                        return value;
                }
                long compl = ~Eval(value);
                if (compl < 0)
                    return string.Format("(0{0}){1}", compl, post);
                return string.Format("{0}{1}", compl, post);
            });
            return expression;
        }

        public void AddHexFormat(string regex) => _hexRegexes.Add(new Regex(regex, RegexOptions.Compiled));

        /// <summary>
        /// Determines if the expression string contains user-defined symbols.
        /// </summary>
        /// <param name="expression">The expression string to evaluate.</param>
        /// <returns><c>True</c> if the expression contains user-defined symbols, otherwise <c>false</c>.</returns>
        bool ContainsSymbols(string expression) =>
                _symbolLookups.Values.Any(l => l.Item1.IsMatch(expression));

        /// <summary>
        /// Evaluate the expression strings for user-defined symbols, then do a callback
        /// to the user-defined lookup to substitute the symbols for a real value.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns>The modified expression string with user-defined symbols replaced
        /// with real values.</returns>
        string EvalDefinedSymbols(string expression)
        {
            // convert client-defined symbols into values
            foreach (var lookup in _symbolLookups)
            {
                Regex r = lookup.Value.Item1;
                var f = lookup.Value.Item2;

                expression = r.Replace(expression, m =>
                {
                    string match = m.Value;
                    if (f != null && !string.IsNullOrEmpty(match))
                        return f(match);
                    return match;
                });
                if (expression.Contains(EVAL_FAIL))
                    return string.Empty;
            }
            return expression;
        }

        /// <summary>
        /// Determines if the character is a math symbol, such as a paranthesis or operator.
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns><c>True</c> if the character is a math symbol, otherwise <c>false</c>.</returns>
        static bool IsMathSymbol(char c)
        {
            return _operators.Contains(c) || c.Equals('(') || c.Equals(')');
        }

        /// <summary>
        /// Convert the math expression string from an infix to postfix (often called
        /// Reverse Polish Notation) expression, using the Shunting Yard strategy.
        /// </summary>
        /// <param name="expression">The infix expression to convert</param>
        /// <returns>A <see cref="System.Collections.Generic.List&lt;string&gt;"/> of outputs
        /// representing a postfix of the infix expression.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        List<string> Shunt(string expression)
        {
            var outputString = new StringBuilder();
            var outputs = new List<string>();
            var operators = new Stack<char>();

            bool lastWasWS = false;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];
                if (char.IsWhiteSpace(c))
                {
                    lastWasWS = true;
                    continue;
                }

                if (char.IsDigit(c) || c.Equals('.'))
                {
                    // check for things like 55 23
                    if (outputString.Length > 0 && lastWasWS)
                        throw new ExpressionException(expression);
                    lastWasWS = false;
                    outputString.Append(c);
                }
                else if (!IsMathSymbol(c))
                {
                    throw new ExpressionException(expression);
                }
                if ((IsMathSymbol(c) || i == expression.Length - 1) && outputString.Length > 0)
                {
                    // If it's a number add it to output
                    outputs.Add(outputString.ToString());
                    outputString.Clear();
                }

                if (_operators.Contains(c))
                {
                    if (i == 0 || _operators.Contains(expression[i - 1]))
                    {
                        // operation is first of string or
                        // previous char is also an operation
                        throw new ExpressionException(expression);
                    }

                    while (operators.Count > 0)
                    {
                        // While there's an operator on the top of the 
                        // stack with greater precedence, pop operators from the 
                        // stack onto the output queue
                        if (_operators.IndexOf(operators.Peek()) >=
                                        _operators.IndexOf(c))
                            outputs.Add(operators.Pop().ToString());
                        else
                            break;
                    }
                    // Push the current operator onto the stack
                    operators.Push(c);
                }
                else if (c.Equals('('))
                {
                    if (i > 0 && !char.IsWhiteSpace(expression[i - 1]) &&
                        !(_operators.Contains(expression[i - 1]) ||
                        expression[i - 1].Equals('('))
                        )
                    {
                        throw new ExpressionException(expression);
                    }
                    // If it's a ( push it onto the stack
                    operators.Push(c);
                }
                else if (c.Equals(')'))
                {

                    if (operators.Count == 0 ||
                        _operators.Contains(expression[i - 1]))
                        throw new ExpressionException(expression);

                    while (operators.Peek() != '(')
                    {
                        // While there's not a ( at the top of the stack:
                        // Pop operators from the stack onto the output queue.
                        outputs.Add(operators.Pop().ToString());
                    }
                    // Pop the left bracket from the stack and discard it
                    operators.Pop();
                }
                else if (!char.IsDigit(c) && !c.Equals('.'))
                {
                    throw new ExpressionException(expression);
                }
            }
            // While there's operators on the stack, pop them to the queue
            while (operators.Count > 0)
            {
                outputs.Add(operators.Pop().ToString());
            }
            return outputs;
        }

        /// <summary>
        /// Internally evaluates expression string as a System.Double. Used primarily by other
        /// functions.
        /// </summary>
        /// <param name="expression">The string expression to evaluate</param>
        /// <returns>A <see cref="T:System.Double"/> representation of the expression.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        double EvalInternal(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                throw new ExpressionException(expression);

            if (_cache.ContainsKey(expression))
                return _cache[expression];

            var pre_eval = PreEvaluate(expression);

            var outputs = Shunt(pre_eval);
            if (outputs.Count == 0)
                throw new ExpressionException(expression);

            try
            {
                var result = Calculate(outputs);
                if (_cache.ContainsKey(expression))
                    _cache[expression] = result;
                return result;
            }
            catch (Exception)
            {
                throw new ExpressionException(expression);
            }
        }

        /// <summary>
        /// Pre-evaluate the math expression string for hex strings, binary strings,
        /// symbols, function calls, and unary operations before final evaluation.
        /// </summary>
        /// <param name="expression">The expression string to pre-evaluate.</param>
        /// <returns>The modified expression string ready for final evaluation.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        string PreEvaluate(string expression)
        {
            string unevaluated = expression;

            expression = expression.Replace("<<", "≪")
                                   .Replace(">>", "≫")
                                   .Replace("==", "≡")
                                   .Replace("&&", "∧")
                                   .Replace("||", "∨")
                                   .Replace("<=", "≤")
                                   .Replace("!=", "≠")
                                   .Replace(">=", "≥");

            foreach (Regex r in _hexRegexes)
                expression = r.Replace(expression, m => Convert.ToInt64(m.Groups[1].Value, 16).ToString());

            expression = _regBinary.Replace(expression, delegate (Match m)
            {
                var binstring = m.Groups[1].Value.Replace("#", "1").Replace(".", "0");
                return Convert.ToInt64(binstring, 2).ToString();
            });

            if (!ContainsSymbols(expression))
                _cache.Add(unevaluated, Double.NaN);

            expression = EvalUnaries(EvalFunctions(EvalDefinedSymbols(expression)));

            foreach (var replacement in _replacements)
                expression = replacement.Item1.Replace(expression, replacement.Item2);

            return expression;
        }

        /// <summary>
        /// Evaluates a text string as a mathematical expression.
        /// </summary>
        /// <param name="expression">The string representation of the mathematical expression.</param>
        /// <returns>The result of the expression evaluation as a <see cref="T:System.Int64"/> value.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        /// <exception cref="T:System.DivideByZeroException">System.DivideByZeroException</exception>
        public long Eval(string expression) => (long)EvalInternal(expression);


        /// <summary>
        /// Calculates a <see cref="System.Collections.Generic.IEnumerable&lt;string&gt;"/> representing a postfix
        /// mathematical expression into a real value.
        /// </summary>
        /// <param name="outputs">A <see cref="System.Collections.Generic.IEnumerable&lt;string&gt;"/></param>
        /// <returns>The calculated value.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        /// <exception cref="T:System.DivideByZeroException">System.DivideByZeroException</exception>"
        double Calculate(IEnumerable<string> outputs)
        {
            var result = new Stack<double>();

            bool needOperator = false;

            foreach (string s in outputs)
            {
                if (double.TryParse(s, out double num))
                {
                    needOperator = result.Count > 0;
                    result.Push(num);
                }
                else
                {
                    needOperator = false;
                    var right = result.Pop();
                    var left = result.Pop();

                    switch (s)
                    {
                        case "+":
                            result.Push(left + right);
                            break;
                        case "-":
                            result.Push(left - right);
                            break;
                        case "*":
                            result.Push(left * right);
                            break;
                        case "/":
                            result.Push(left / right);
                            break;
                        case "&":
                            result.Push((long)left & (long)right);
                            break;
                        case "|":
                            result.Push((long)left | (long)right);
                            break;
                        case "^":
                            result.Push((long)left ^ (long)right);
                            break;
                        case "%":
                            result.Push((long)left % (long)right);
                            break;
                        case "≪":
                            result.Push((int)left << (int)right);
                            break;
                        case "≫":
                            result.Push((int)left >> (int)right);
                            break;
                        case "↑":
                            result.Push(Math.Pow(left, right));
                            break;
                        case ">":
                            result.Push(left > right ? 1 : 0);
                            break;
                        case "≥":
                            result.Push(left >= right ? 1 : 0);
                            break;
                        case "≡":
                            result.Push((long)left == (long)right ? 1 : 0);
                            break;
                        case "≠":
                            result.Push((long)left != (long)right ? 1 : 0);
                            break;
                        case "≤":
                            result.Push(left <= right ? 1 : 0);
                            break;
                        case "<":
                            result.Push(left < right ? 1 : 0);
                            break;
                        case "∧":
                            result.Push((int)left & (int)right);
                            break;
                        case "∨":
                            result.Push((int)left | (int)right);
                            break;
                        default:
                            throw new Exception();
                    }
                }
            }
            if (needOperator) throw new Exception();
            return result.Pop();
        }

        /// <summary>
        /// Evaluates a text string as a mathematical expression.
        /// </summary>
        /// <param name="expression">The string representation of the mathematical expression.</param>
        /// <param name="minval">The minimum value of expression. If the evaluated value is
        /// lower, a System.OverflowException will occur.</param>
        /// <param name="maxval">The maximum value of the expression. If the evaluated value 
        /// is higher, a System.OverflowException will occur.</param>
        /// <returns>The result of the expression evaluation as a <see cref="T:System.Int64"/> value.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        /// <exception cref="T:System.DivideByZeroException">System.DivideByZeroException</exception>
        /// <exception cref="T:System.OverflowException">System.OverflowException</exception>
        public long Eval(string expression, long minval, long maxval)
        {
            var result = EvalInternal(expression);
            if (double.IsInfinity(result))
                throw new DivideByZeroException(expression);
            if (result < minval || result > maxval)
                throw new OverflowException(expression);
            return (long)result;
        }

        /// <summary>
        /// Evaluates a text string as a conditional (boolean) evaluation.
        /// </summary>
        /// <param name="condition">The string representation of the conditional expression.</param>
        /// <returns><c>True</c>, if the expression is true, otherwise <c>false</c>.</returns>
        /// <exception cref="T:DotNetAsm.ExpressionException">DotNetAsm.ExpressionException</exception>
        public bool EvalCondition(string condition) => Eval(condition) == 1;

        /// <summary>
        /// Defines a symbol lookup for the evaluator to translate symbols (such as 
        /// variables) in expressions.
        /// </summary>
        /// <param name="pattern">A regex pattern for the symbol</param>
        /// <param name="lookupfunc">The lookup function to define the symbol</param>
        /// <exception cref="T:System.ArgumentNullException">System.ArgumentNullException</exception>
        public void DefineSymbolLookup(string pattern, Func<string, string> lookupfunc)
        {
            var value = new Tuple<Regex, Func<string, string>>(new Regex(pattern, RegexOptions.Compiled), lookupfunc);
            if (_symbolLookups.ContainsKey(pattern))
                _symbolLookups[pattern] = value;
            else
                _symbolLookups.Add(pattern, value);
        }
        #endregion
    }
}
