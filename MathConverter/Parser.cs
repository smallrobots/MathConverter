﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HexInnovation
{
    class Parser : IDisposable
    {
        public static AbstractSyntaxTree[] Parse(string expression)
        {
            using (var p = new Parser(expression))
            {
                try
                {
                    return p.ConverterParameter().ToArray();
                }
                catch (ParsingException e)
#if DEBUG
#pragma warning disable CS7095 // Filter expression is a constant
                when (false)
#pragma warning restore CS7095 // Filter expression is a constant
#endif

                {
                    throw new Exception($"Failed to parse the expression:{Environment.NewLine}{expression}{Environment.NewLine}See the inner exception for details.", e);
                }
            }
        }


        private Scanner scanner;
        private Parser(string expression)
        {
            scanner = new Scanner(this, expression);
        }
        internal AbstractSyntaxTree ParseInterpolatedStringArg()
        {
            var result = Conditional();

            var t = scanner.Peek();

            switch (t.TokenType)
            {
                case TokenType.RCurlyBracket:
                case TokenType.Colon:
                    return result;
                default:
                    throw new ParsingException(scanner.Position, "Error parsing interpolated string. Could not find closing curly bracket (or a colon) after the argument.");
            }

        }
        private IEnumerable<AbstractSyntaxTree> ConverterParameter()
        {
            while (true)
            {
                var result = Conditional();
                var t = scanner.GetToken();

                switch (t.TokenType)
                {
                    case TokenType.EOF:
                        yield return result;
                        yield break;
                    case TokenType.Semicolon:
                        yield return result;
                        break;
                    default:
                        throw new ParsingException(scanner.Position, "The conversion parameter could not be parsed to a valid string.");
                }
            }
        }
        private AbstractSyntaxTree Conditional()
        {
            return Conditional(NullCoalescing());
        }
        private AbstractSyntaxTree Conditional(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.QuestionMark:
                    var then = Conditional();
                    t = scanner.GetToken();
                    switch (t.TokenType)
                    {
                        case TokenType.Colon:
                            return Conditional(new TernaryNode(e, then, Conditional()));
                        default:
                            throw new ParsingException(scanner.Position, "Could not find the ':' to terminate the ternary ('?:') statement");
                    }
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree NullCoalescing()
        {
            return NullCoalescing(ConditionalOr());
        }
        private AbstractSyntaxTree NullCoalescing(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.DoubleQuestionMark:
                    return NullCoalescing(new NullCoalescingNode(e, ConditionalOr()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree ConditionalOr()
        {
            return ConditionalOr(ConditionalAnd());
        }
        private AbstractSyntaxTree ConditionalOr(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.Or:
                    return ConditionalOr(new OrNode(e, ConditionalAnd()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree ConditionalAnd()
        {
            return ConditionalAnd(Equality());
        }
        private AbstractSyntaxTree ConditionalAnd(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.And:
                    return ConditionalAnd(new AndNode(e, Equality()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree Equality()
        {
            return Equality(Relational());
        }
        private AbstractSyntaxTree Equality(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.DoubleEqual:
                    return Equality(new EqualNode(e, Relational()));
                case TokenType.NotEqual:
                    return Equality(new NotEqualNode(e, Relational()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree Relational()
        {
            return Relational(Additive());
        }
        private AbstractSyntaxTree Relational(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.LessThan:
                    return Relational(new LessThanNode(e, Additive()));
                case TokenType.LessThanEqual:
                    return Relational(new LessThanEqualNode(e, Additive()));
                case TokenType.GreaterThan:
                    return Relational(new GreaterThanNode(e, Additive()));
                case TokenType.GreaterThanEqual:
                    return Relational(new GreaterThanEqualNode(e, Additive()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree Additive()
        {
            return Additive(Multiplicative());
        }
        private AbstractSyntaxTree Additive(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.Plus:
                    return Additive(new AddNode(e, Multiplicative()));
                case TokenType.Minus:
                    return Additive(new SubtractNode(e, Multiplicative()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree Multiplicative()
        {
            return Multiplicative(Exponent());
        }
        private AbstractSyntaxTree Multiplicative(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.Modulo:
                    return Multiplicative(new ModuloNode(e, Exponent()));
                case TokenType.Times:
                    return Multiplicative(new MultiplyNode(e, Exponent()));
                case TokenType.Divide:
                    return Multiplicative(new DivideNode(e, Exponent()));
                case TokenType.X:
                case TokenType.Y:
                case TokenType.Z:
                case TokenType.Lexical:
                case TokenType.LBracket:
                case TokenType.LParen:
                    scanner.PutBackToken();
                    return Multiplicative(new MultiplyNode(e, Exponent()));
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }

        private AbstractSyntaxTree Exponent()
        {
            return Exponent(Primary());
        }
        private AbstractSyntaxTree Exponent(AbstractSyntaxTree e)
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.Caret:
                    return Exponent(new ExponentNode(e, Primary()));
                case TokenType.Number:
                    if (e is VariableNode)
                    {
                        return Exponent(new ExponentNode(e, new ConstantNumberNode(double.Parse((t as LexicalToken).Lex, NumberStyles.Number, CultureInfo.InvariantCulture))));
                    }
                    else
                    {
                        scanner.PutBackToken();
                        return e;
                    }
                default:
                    scanner.PutBackToken();
                    return e;
            }
        }
        private AbstractSyntaxTree Primary()
        {
            var t = scanner.GetToken();

            switch (t.TokenType)
            {
                case TokenType.Number:
                    return new ConstantNumberNode(double.Parse((t as LexicalToken).Lex, NumberStyles.Number, CultureInfo.InvariantCulture));
                case TokenType.Minus:
                    return new NegativeNode(Primary());
                case TokenType.Not:
                    return new NotNode(Primary());
                case TokenType.X:
                    return new VariableNode(0);
                case TokenType.Y:
                    return new VariableNode(1);
                case TokenType.Z:
                    return new VariableNode(2);
                case TokenType.String:
                    return new StringNode((t as LexicalToken).Lex);
                case TokenType.InterpolatedString:
                    var token = t as InterpolatedStringToken;
                    return new FormulaNodeN("Format", FormulaNodeN.Format, new AbstractSyntaxTree[] { new StringNode(token.Lex) }.Union(token.Arguments));

                case TokenType.Lexical:
                    var lex = (t as LexicalToken).Lex;
                    Func<object> formula0 = null;
                    Func<double, double> formula1 = null;
                    Func<object, object, object> formula2 = null;
                    Func<IEnumerable<object>, object> formulaN = null;
                    switch (lex.ToLower())
                    {
                        case "null":
                            return new NullNode();
                        case "true":
                            return new ValueNode(true);
                        case "false":
                            return new ValueNode(false);
                        default:
                            switch (lex.ToLower())
                            {
                                case "now":
                                    formula0 = () => DateTime.Now;
                                    break;
                                case "cos":
                                    formula1 = Math.Cos;
                                    break;
                                case "sin":
                                    formula1 = Math.Sin;
                                    break;
                                case "tan":
                                    formula1 = Math.Tan;
                                    break;
                                case "abs":
                                    formula1 = Math.Abs;
                                    break;
                                case "acos":
                                case "arccos":
                                    formula1 = Math.Acos;
                                    break;
                                case "asin":
                                case "arcsin":
                                    formula1 = Math.Asin;
                                    break;
                                case "atan":
                                case "arctan":
                                    formula1 = Math.Atan;
                                    break;
                                case "ceil":
                                case "ceiling":
                                    formula1 = Math.Ceiling;
                                    break;
                                case "floor":
                                    formula1 = Math.Floor;
                                    break;
                                case "sqrt":
                                    formula1 = Math.Sqrt;
                                    break;
                                case "degrees":
                                case "deg":
                                    formula1 = x => x / Math.PI * 180;
                                    break;
                                case "radians":
                                case "rad":
                                    formula1 = x => x / 180 * Math.PI;
                                    break;
                                case "round":
                                    formula2 = (x, y) =>
                                    {
                                        var a = MathConverter.ConvertToDouble(x);
                                        var b = MathConverter.ConvertToDouble(y);
                                        if (a.HasValue && b.HasValue)
                                        {
                                            if (b.Value == (int)b.Value)
                                                return Math.Round(a.Value, (int)b.Value);
                                            else
                                                throw new Exception(string.Format("Error calling Math.Round({0}, {1}):\r\n{1} is not an integer.", a, y));
                                        }
                                        else
                                        {
                                            return null;
                                        }
                                    };
                                    break;
                                case "atan2":
                                case "arctan2":
                                    formula2 = (x, y) =>
                                    {
                                        var a = MathConverter.ConvertToDouble(x);
                                        var b = MathConverter.ConvertToDouble(y);
                                        if (a.HasValue && b.HasValue)
                                            return Math.Atan2(a.Value, b.Value);
                                        else
                                            return null;
                                    };
                                    break;
                                case "log":
                                    formula2 = (x, y) =>
                                    {
                                        var a = MathConverter.ConvertToDouble(x);
                                        var b = MathConverter.ConvertToDouble(y);
                                        if (a.HasValue && b.HasValue)
                                            return Math.Log(a.Value, b.Value);
                                        else
                                            return null;
                                    };
                                    break;
                                case "isnull":
                                case "ifnull":
                                    formula2 = (x, y) => ReferenceEquals(x, null) ? y : x;
                                    break;
                                case "and":
                                    formulaN = FormulaNodeN.And;
                                    break;
                                case "nor":
                                    formulaN = FormulaNodeN.Nor;
                                    break;
                                case "or":
                                    formulaN = FormulaNodeN.Or;
                                    break;
                                case "max":
                                    formulaN = FormulaNodeN.Max;
                                    break;
                                case "min":
                                    formulaN = FormulaNodeN.Min;
                                    break;
                                case "avg":
                                case "average":
                                    formulaN = FormulaNodeN.Average;
                                    break;
                                case "pi":
                                    return new ConstantNumberNode(Math.PI);
                                case "e":
                                    return new ConstantNumberNode(Math.E);
                                case "format":
                                    formulaN = FormulaNodeN.Format;
                                    break;
                                case "concat":
                                    formulaN = FormulaNodeN.Concat;
                                    break;
                                case "join":
                                    formulaN = FormulaNodeN.Join;
                                    break;
                                case "contains":
                                    formula2 = (x, y) =>
                                    {
                                        if (x is IEnumerable<dynamic>)
                                        {
                                            return (x as IEnumerable<dynamic>).Contains(y);
                                        }
                                        else if (x is string)
                                        {
                                            return (x as string).Contains(y as dynamic);
                                        }
                                        else
                                        {
                                            return null;
                                        }
                                    };
                                    break;
                                default:
                                    var err = $"{lex} is an invalid formula name";
                                    throw new ParsingException(scanner.Position, err, new NotSupportedException(err));
                            }
                            break;
                    }

                    if (formula0 != null)
                    {
                        var ex = $"{lex} is a formula that takes zero arguments. You must call it like this: \"{lex}()\"";

                        if (scanner.GetToken().TokenType != TokenType.LParen)
                            throw new ParsingException(scanner.Position, ex);
                        if (scanner.GetToken().TokenType != TokenType.RParen)
                            throw new ParsingException(scanner.Position, ex);

                        return new FormulaNode0(lex, formula0);
                    }
                    else if (formula1 != null)
                    {
                        // Create a formula1.
                        var ex = $"{lex} is a formula that takes one argument.  You must specify the arguments like this: \"{lex}(3)\"";

                        if (scanner.GetToken().TokenType != TokenType.LParen)
                            throw new ParsingException(scanner.Position, ex);

                        AbstractSyntaxTree arg;

                        try
                        {
                            arg = Conditional();
                        }
                        catch (Exception e)
                        {
                            throw new ParsingException(scanner.Position, ex, new Exception(ex, e));
                        }

                        if (scanner.GetToken().TokenType != TokenType.RParen)
                            throw new ParsingException(scanner.Position, ex);

                        return new FormulaNode1(lex, formula1, arg);
                    }
                    else if (formula2 != null)
                    {
                        // Create a formula2.
                        var ex = $"{lex} is a formula that takes two argument.  You must specify the arguments like this: \"{lex}(3;2)\"";
                        if (lex.ToLower() == "round")
                            ex = "round is a formula that takes one or two argments. You must specify the argument(s) like this: round(4.693) or round(4.693;2)";

                        if (scanner.GetToken().TokenType != TokenType.LParen)
                            throw new ParsingException(scanner.Position, ex);

                        AbstractSyntaxTree arg1, arg2;

                        try
                        {
                            arg1 = Conditional();
                        }
                        catch (Exception inner)
                        {
                            throw new ParsingException(scanner.Position, ex, inner);
                        }

                        switch (scanner.GetToken().TokenType)
                        {
                            case TokenType.Semicolon:
                                try
                                {
                                    arg2 = Conditional();
                                }
                                catch (Exception inner)
                                {
                                    throw new ParsingException(scanner.Position, ex, inner);
                                }

                                if (scanner.GetToken().TokenType == TokenType.RParen)
                                {
                                    return new FormulaNode2(lex, formula2, arg1, arg2);
                                }

                                break;
                            case TokenType.RParen:
                                if (lex.ToLower() == "round")
                                {
                                    return new FormulaNode2(lex, formula2, arg1, new ConstantNumberNode(0));
                                }
                                break;
                        }
                        throw new ParsingException(scanner.Position, ex);
                    }
                    else
                    {
                        // Create a formulaN.
                        if (scanner.GetToken().TokenType != TokenType.LParen)
                            throw new ParsingException(scanner.Position, $"You must specify arguments for {lex}.  Those arguments must be enclosed in parentheses.");

                        var trees = new List<AbstractSyntaxTree>();

                        if (scanner.GetToken().TokenType == TokenType.RParen)
                        {
                            throw new ParsingException(scanner.Position, $"You must specify at least one argument for {lex}.");
                        }
                        else
                        {
                            scanner.PutBackToken();
                        }

                        while (true)
                        {
                            try
                            {
                                trees.Add(Conditional());
                            }
                            catch (Exception e)
                            {
                                throw new ParsingException(scanner.Position, $"Error parsing arguments for {lex}.", e);
                            }

                            var type = scanner.GetToken().TokenType;
                            switch (type)
                            {
                                case TokenType.RParen:
                                    return new FormulaNodeN(lex, formulaN, trees);
                                case TokenType.Semicolon:
                                    break;
                                default:
                                    throw new ParsingException(scanner.Position, $"Error parsing arguments for {lex}. Invalid character: {type}. Expected either a comma, semicolon, or right parenthesis.");
                            }
                        }
                    }
                case TokenType.LBracket:
                    t = scanner.GetToken();
                    var exc = new Exception("Variable accessors should come in the form [i], where i is an integer.");
                    int i;
                    if (t is LexicalToken)
                    {
                        if (int.TryParse((t as LexicalToken).Lex, out i))
                        {
                            try
                            {
                                return new VariableNode(i);
                            }
                            finally
                            {
                                if (scanner.GetToken().TokenType != TokenType.RBracket)
                                    throw new ParsingException(scanner.Position, exc.Message, exc);
                            }
                        }
                    }
                    throw exc;
                case TokenType.LParen:
                    var cond = Conditional();
                    if (scanner.GetToken().TokenType != TokenType.RParen)
                        throw new ParsingException(scanner.Position, "Mismatching parentheses");

                    return cond;
                default:
                    throw new ParsingException(scanner.Position, "Invalid conversion string.");
            }
        }

        ~Parser()
        {
            Dispose();
        }
        public void Dispose()
        {
            scanner.Dispose();
        }
    }
}
