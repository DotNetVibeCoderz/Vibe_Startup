using System.Globalization;

namespace AppBender.Core.Common;

/// <summary>
/// Small recursive-descent evaluator for arithmetic expressions:
/// numbers, + - * / % ^, parentheses, and functions
/// (abs, sqrt, round, floor, ceil, min, max, pow, sin, cos, tan, log, log10, exp),
/// plus comparisons returning 1/0 (gt, gte, lt, lte, eq, neq) and if(cond, then, else)
/// so calculators can express conditional logic, e.g. if(gte(total, nisab), total*0.025, 0).
/// </summary>
public static class MathEvaluator
{
    public static double Evaluate(string expression)
    {
        var parser = new Parser(expression);
        var value = parser.ParseExpression();
        parser.ExpectEnd();
        return value;
    }

    private class Parser(string text)
    {
        private int _pos;

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWs();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParsePower();
            while (true)
            {
                SkipWs();
                if (Match('*')) value *= ParsePower();
                else if (Match('/')) value /= ParsePower();
                else if (Match('%')) value %= ParsePower();
                else return value;
            }
        }

        private double ParsePower()
        {
            var value = ParseUnary();
            SkipWs();
            if (Match('^')) return Math.Pow(value, ParsePower());
            return value;
        }

        private double ParseUnary()
        {
            SkipWs();
            if (Match('-')) return -ParseUnary();
            if (Match('+')) return ParseUnary();
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWs();
            if (Match('('))
            {
                var value = ParseExpression();
                SkipWs();
                if (!Match(')')) throw new FormatException("Expected ')'");
                return value;
            }

            if (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.'))
            {
                var start = _pos;
                while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.')) _pos++;
                return double.Parse(text[start.._pos], CultureInfo.InvariantCulture);
            }

            if (_pos < text.Length && char.IsLetter(text[_pos]))
            {
                var start = _pos;
                while (_pos < text.Length && char.IsLetter(text[_pos])) _pos++;
                var name = text[start.._pos].ToLowerInvariant();
                if (name == "pi") return Math.PI;
                if (name == "e") return Math.E;
                SkipWs();
                if (!Match('(')) throw new FormatException($"Unknown identifier '{name}'");
                var args = new List<double> { ParseExpression() };
                SkipWs();
                while (Match(',')) { args.Add(ParseExpression()); SkipWs(); }
                if (!Match(')')) throw new FormatException("Expected ')'");
                return name switch
                {
                    "abs" => Math.Abs(args[0]),
                    "sqrt" => Math.Sqrt(args[0]),
                    "round" => args.Count > 1 ? Math.Round(args[0], (int)args[1]) : Math.Round(args[0]),
                    "floor" => Math.Floor(args[0]),
                    "ceil" => Math.Ceiling(args[0]),
                    "min" => args.Min(),
                    "max" => args.Max(),
                    "pow" => Math.Pow(args[0], args[1]),
                    "sin" => Math.Sin(args[0]),
                    "cos" => Math.Cos(args[0]),
                    "tan" => Math.Tan(args[0]),
                    "log" => args.Count > 1 ? Math.Log(args[0], args[1]) : Math.Log(args[0]),
                    "log10" => Math.Log10(args[0]),
                    "exp" => Math.Exp(args[0]),
                    "gt" => args[0] > args[1] ? 1 : 0,
                    "gte" => args[0] >= args[1] ? 1 : 0,
                    "lt" => args[0] < args[1] ? 1 : 0,
                    "lte" => args[0] <= args[1] ? 1 : 0,
                    "eq" => Math.Abs(args[0] - args[1]) < 1e-9 ? 1 : 0,
                    "neq" => Math.Abs(args[0] - args[1]) >= 1e-9 ? 1 : 0,
                    "if" => args[0] != 0 ? args[1] : (args.Count > 2 ? args[2] : 0),
                    _ => throw new FormatException($"Unknown function '{name}'")
                };
            }

            throw new FormatException($"Unexpected character at position {_pos}");
        }

        public void ExpectEnd()
        {
            SkipWs();
            if (_pos < text.Length) throw new FormatException($"Unexpected '{text[_pos]}'");
        }

        private bool Match(char c)
        {
            if (_pos < text.Length && text[_pos] == c) { _pos++; return true; }
            return false;
        }

        private void SkipWs()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
        }
    }
}
