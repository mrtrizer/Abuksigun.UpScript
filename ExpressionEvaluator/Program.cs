using System;
using System.Collections.Generic;
using System.Reflection;

public enum TokenType { Block, Skip, Literal, Reference, Binary, Unary }

public record Token(TokenType Type, object Value, int StartIndex, int Length, List<Token> Children)
{
    public TokenType Type { get; set; } = Type;
    public object Value { get; set; } = Value;
    public int Length { get; set; } = Length;
}

public class Parser
{
    public class ParserException : Exception
    {
        public ParserException(string message) : base(message) { }
    }

    record Method(Delegate Func, Type ReturnType);
    public record CompilationResult(Type Type, List<object> Flow);

    readonly string input;
    int position = 0;
    Stack<Token> stack = new();
    public Dictionary<string, object> Variables { get; } = new();

    public Parser(string input, Dictionary<string, object> variables)
    {
        this.input = input;
        Variables = variables;
    }

    Token AddToken(TokenType tokenType, object value, int StartIndex, int Length)
    {
        if (tokenType == TokenType.Skip)
            return null;
        var token = new Token(tokenType, value, StartIndex, Length, new());
        stack.Peek().Children.Add(token);
        return token;
    }
    private bool Space()
    {
        while (position < input.Length && char.IsWhiteSpace(input[position]))
            position++;
        return true;
    }

    bool ZeroOrMore(params Func<bool>[] funcs)
    {
        while (Array.TrueForAll(funcs, func => func())) { /* Nothing */ }
        return true;
    }
    bool Or(params Func<bool>[] args)
    {
        int start = position;
        foreach (var func in args)
        {
            if (func())
                return true;
            else
                position = start;
        }
        return false;
    }
    bool And(params Func<bool>[] args)
    {
        int start = position;
        foreach (var func in args)
        {
            if (!func())
            {
                position = start;
                return false;
            }
        }
        return true;
    }

    bool Block(Func<bool> arg, TokenType tokenType = TokenType.Block, Func<string, object> parse = null)
    {
        stack.Push(AddToken(TokenType.Block, null, position, 0));

        bool result = arg();

        var token = stack.Pop();
        if (result)
        {
            token.Type = tokenType;
            token.Length = position - token.StartIndex;
            if (parse != null)
                token.Value = parse(input.Substring(token.StartIndex, token.Length));
            if (tokenType == TokenType.Block)
            {
                var parent = stack.Peek();
                if (token.Children.Count <= 1)
                    parent.Children.Remove(token);
                if (token.Children.Count == 1)
                    parent.Children.Add(token.Children[0]);
            }
        }
        else
        {
            var parent = stack.Peek();
            parent.Children.Remove(token);
        }

        return result;
    }

    bool Match(string str, TokenType tokenType = TokenType.Skip)
    {
        if (position + str.Length - 1 < input.Length && input.AsSpan(position, str.Length).SequenceEqual(str.AsSpan()))
        {
            AddToken(tokenType, null, position, str.Length);
            position += str.Length;
            return true;
        }
        return false;
    }

    bool IncPosition => position++ < input.Length;
    bool Range(char start, char end) => input.Length > position && input[position] >= start && input[position] <= end && IncPosition;
    bool Character() => input.Length > position && char.IsAsciiLetter(input[position]) && IncPosition;

    bool Digits => And(() => Range('0', '9'), () => ZeroOrMore(() => Range('0', '9')));
    bool Letter => Or(() => Range('a', 'z'), () => Range('A', 'Z'));
    bool Integer => Block(() => Digits, TokenType.Literal, x => int.Parse(x));
    bool Float => Block(() => And(() => Digits, () => Match("."), () => Digits), TokenType.Literal, x => float.Parse(x));
    bool NumberLiteral => Or(() => Float, () => Integer);
    bool BoolLiteral => Block(() => Or(() => Match("true"), () => Match("false")), TokenType.Literal, x => bool.Parse(x));
    bool StringLiteral => Block(() => And(() => Match("\""), () => ZeroOrMore(() => Character()), () => Match("\"")), TokenType.Literal, x => x[1..^1]);

    bool Reference => Block(() => And(() => Letter, () => ZeroOrMore(() => Or(() => Letter, () => Range('0', '9')))), TokenType.Reference, x => x);
    bool BracketBlock => Block(() => And(() => Match("("), () => Logical, () => Match(")")));
    bool Factor => Block(() => And(() => Space(), () => Or(() => Match("-", TokenType.Unary), () => Match("!", TokenType.Unary), () => true), () => Or(() => NumberLiteral, () => StringLiteral, () => BoolLiteral, () => Reference, () => BracketBlock) && Space()));
    
    bool Term => Block(() => And(() => Factor, () => ZeroOrMore(() => Or(() => Match("*", TokenType.Binary), () => Match("/", TokenType.Binary), () => Match("%", TokenType.Binary)), () => Factor)));
    bool Expression => Block(() => And(() => Term, () => ZeroOrMore(() => Or(() => Match("+", TokenType.Binary), () => Match("-", TokenType.Binary)), () => Term)));
    bool Comparison => Block(() => And(() => Expression, () => ZeroOrMore(() => Or(() => Match("<", TokenType.Binary), () => Match("<=", TokenType.Binary), () => Match(">", TokenType.Binary), () => Match(">=", TokenType.Binary), () => Match("==", TokenType.Binary), () => Match("!=", TokenType.Binary)), () => Expression)));
    bool Logical => Block(() => And(() => Comparison, () => ZeroOrMore(() => Or(() => Match("&&", TokenType.Binary), () => Match("||", TokenType.Binary)), () => Comparison)));

    static void PrintToken(string input, Token token, int level = 0)
    {
        Console.WriteLine($"{new string(' ', level * 2)}{token.Type} {input.Substring(token.StartIndex, token.Length)}");
        foreach (var child in token.Children)
            PrintToken(input, child, level + 1);
    }

    public Token Parse()
    {
        stack.Push(new Token(TokenType.Block, null, 0, 0, new()));
        bool success = Logical;
        var root = stack.Pop().Children.First();
        PrintToken(input, root);
        if (!success || root.Length != input.Length)
        {
            while (root.Children.Count > 0)
                root = root.Children.Last();
            throw new ParserException($"Unexpect token at: {root.StartIndex + root.Length}\n{input.Substring(0, root.StartIndex)}###");
        }
        return root;
    }

    static Dictionary<string, string> binaryOperators = new()
    {
        { "+", "op_Addition"},
        { "-", "op_Subtraction"},
        { "*", "op_Multiply"},
        { "/", "op_Division"},
        { "%", "op_Modulus"},
        { "<", "op_LessThan"},
        { "<=","op_LessThanOrEqual"},
        { ">", "op_GreaterThan"},
        { ">=","op_GreaterThanOrEqual"},
        { "==","op_Equality"},
        { "!=","op_Inequality"},
        { "&&","op_BitwiseAnd"},
        { "||","op_BitwiseOr"}
    };

    static Dictionary<string, string> unaryOperators = new()
    {
        { "-", "op_UnaryNegation" },
        { "!", "op_LogicalNot" },
    };

    static Dictionary<string, Delegate[]> fastOperators = new()
    {
        { "op_Addition", new Delegate[] { (int a, int b) => a + b, (double a, double b) => a + b, (float a, float b) => a + b, (long a, long b) => a + b, (string a, string b) => a + b, (char a, char b) => a.ToString() + b.ToString() } },
        { "op_Subtraction", new Delegate[] { (int a, int b) => a - b, (double a, double b) => a - b, (float a, float b) => a - b, (long a, long b) => a - b } },
        { "op_Multiply", new Delegate[] { (int a, int b) => a * b, (double a, double b) => a * b, (float a, float b) => a * b, (long a, long b) => a * b } },
        { "op_Division", new Delegate[] { (int a, int b) => a / b, (double a, double b) => a / b, (float a, float b) => a / b, (long a, long b) => a / b } },
        { "op_Modulus", new Delegate[] { (int a, int b) => a % b, (double a, double b) => a % b, (float a, float b) => a % b, (long a, long b) => a % b } },
        { "op_LessThan", new Delegate[] { (int a, int b) => a < b, (double a, double b) => a < b, (float a, float b) => a < b, (long a, long b) => a < b } },
        { "op_LessThanOrEqual", new Delegate[] { (int a, int b) => a <= b, (double a, double b) => a <= b, (float a, float b) => a <= b, (long a, long b) => a <= b } },
        { "op_GreaterThan", new Delegate[] { (int a, int b) => a > b, (double a, double b) => a > b, (float a, float b) => a > b, (long a, long b) => a > b } },
        { "op_GreaterThanOrEqual", new Delegate[] { (int a, int b) => a >= b, (double a, double b) => a >= b, (float a, float b) => a >= b, (long a, long b) => a >= b } },
        { "op_Equality", new Delegate[] { (int a, int b) => a == b, (double a, double b) => a == b, (float a, float b) => a == b, (long a, long b) => a == b, (bool a, bool b) => a == b, (string a, string b) => a == b, (char a, char b) => a == b } },
        { "op_Inequality", new Delegate[] { (int a, int b) => a != b, (double a, double b) => a != b, (float a, float b) => a != b, (long a, long b) => a != b, (bool a, bool b) => a != b, (string a, string b) => a != b, (char a, char b) => a != b } },
        { "op_BitwiseAnd", new Delegate[] { (int a, int b) => a & b, (long a, long b) => a & b, (bool a, bool b) => a && b } }, // For int, long, and bool
        { "op_BitwiseOr", new Delegate[] { (int a, int b) => a | b, (long a, long b) => a | b, (bool a, bool b) => a || b } }, // For int, long, and bool
        { "op_UnaryNegation", new Delegate[] { (int a) => -a, (double a) => -a, (float a) => -a, (long a) => -a } },
        { "op_LogicalNot", new Delegate[] { (bool a) => !a } }, // Only for bool
        { "op_Implicit", new Delegate[] { (int a) => (float)a, (float a) => (double)a, (char a) => (int)a, (int a) => a.ToString(), (float a) => a.ToString(), (double a) => a.ToString(), (bool a) => a.ToString() } },
        { "op_Explicit", new Delegate[] { (float a) => (int)a, (double a) => (float)a, (int a) => (char)a } }
    };

    static Method FindMethod(string name, params Type[] arguments)
    {
        foreach (var func in fastOperators[name])
        {
            var parameters = func.Method.GetParameters();
            if (parameters.Length == arguments.Length && parameters.Select(x => x.ParameterType).SequenceEqual(arguments))
                return new(func, func.Method.ReturnType);
        }

        if (arguments.First().GetMethod(name, arguments) is { } method)
        {
            return new(method.GetParameters().Length switch
            {
                0 => () => method.Invoke(null, null),
                1 => (object a) => method.Invoke(null, new[] { a }),
                2 => (object a, object b) => method.Invoke(null, new[] { a, b }),
                3 => (object a, object b, object c) => method.Invoke(null, new[] { a, b, c }),
                4 => (object a, object b, object c, object d) => method.Invoke(null, new[] { a, b, c, d }),
                5 => (object a, object b, object c, object d, object e) => method.Invoke(null, new[] { a, b, c, d, e }),
            }, method.ReturnType);
        }
        return null;
    }

    static List<Method> FindImplicitConversions(Type type)
    {
        List<Method> conversions = new List<Method>();

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var method in methods)
        {
            if (method.Name == "op_Implicit" && method.ReturnType != type && method.GetParameters()[0].ParameterType == type)
                conversions.Add(new ((object a) => method.Invoke(null, new[] { a }), method.ReturnType));
        }

        foreach (var func in fastOperators["op_Implicit"])
        {
            var parameters = func.Method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == type)
                conversions.Add(new (func, func.Method.ReturnType));
        }

        return conversions;
    }

    CompilationResult AddMethod(string name, params CompilationResult[] arguments)
    {
        List<object> flow = new();
        var func = FindMethod(name, arguments.Select(x => x.Type).ToArray());
        if (func == null)
        {
            var allConversions = arguments.Select(x => FindImplicitConversions(x.Type)).ToList();

            // Generate all combinations of implicit conversions
            List<Method[]> combinations = new();
            GenerateCombinations(allConversions, new Method[allConversions.Count], combinations, 0);

            foreach (var combination in combinations)
            {
                func = FindMethod(name, combination.Select((x, i) => x?.ReturnType ?? arguments[i].Type).ToArray());
                if (func != null)
                {
                    for (int i = 0; i < combination.Length; i++)
                    {
                        flow.AddRange(arguments[i].Flow);
                        if (combination[i] != null)
                            flow.Add(combination[i].Func);
                    }
                    break;
                }
            }
        }
        else
        {
            flow.AddRange(arguments.SelectMany(x => x.Flow));
        }

        if (func == null)
            throw new ParserException($"Method {name} not found for types {string.Join(", ", arguments.Select(x => x.Type.Name))}");

        flow.Add(func.Func);
        return new(func.ReturnType, flow);
    }

    void GenerateCombinations(List<List<Method>> allConversions, Method[] current, List<Method[]> combinations, int argIndex)
    {
        foreach (var argument in allConversions[argIndex].Append(null))
        {
            current[argIndex] = argument;
            if (argIndex == allConversions.Count - 1)
                combinations.Add(current.ToArray());
            else
                GenerateCombinations(allConversions, current, combinations, argIndex + 1);
        }
    }


    public CompilationResult Compile(Token token)
    {
        try
        {
            if (token.Type == TokenType.Literal)
            {
                return new(token.Value.GetType(), new List<object>() { token.Value });
            }
            else if (token.Type == TokenType.Reference)
            {
                return new(Variables[token.Value as string].GetType(), new List<object>() { Variables[token.Value as string] });
            }
            else if (token.Type == TokenType.Block)
            {
                var child = token.Children[0];
                if (child.Type == TokenType.Unary)
                {
                    CompilationResult r1 = Compile(token.Children[1]);
                    return AddMethod(unaryOperators[input.Substring(child.StartIndex, child.Length)], r1);
                }
                else
                {
                    CompilationResult r1 = Compile(token.Children[0]);
                    for (int i = 1; i < token.Children.Count; i++)
                    {
                        child = token.Children[i];
                        if (child.Type == TokenType.Binary || child.Type == TokenType.Unary)
                        {
                            if (child.Type == TokenType.Binary)
                            {
                                CompilationResult r2 = Compile(token.Children[i + 1]);
                                r1 = AddMethod(binaryOperators[input.Substring(child.StartIndex, child.Length)], r1, r2);
                            }
                        }
                    }
                    return r1;
                }
            }
        }
        catch (Exception e)
        {
#if DEBUG
            if (e is ParserException)
#endif
                throw;
        }
        throw new ParserException($"Invalid expression at: {token.StartIndex}\n{input.Substring(0, token.StartIndex)}###");
    }
}

public static class Program
{

    public static void Main()
    {
        //{
        //    string input = "-2 + test * 20 +20 + 2+3*4* -(5 + 6)";
        //    Parser parser = new Parser(input, new() { { "test", 10 } });
        //    var root = parser.Parse();
        //    var instructions = parser.Compile(root).Flow;
        //    Console.WriteLine($"Result: {Run(instructions)}");
        //}
        //{
        //    string input = "(10.0 - -20) == 30 && (test * 10 == 100)";
        //    Parser parser = new Parser(input, new() { { "test", 10 } });
        //    var root = parser.Parse();
        //    var instructions = parser.Compile(root).Flow;
        //    Console.WriteLine($"Result: {Run(instructions)}");
        //}
        {
            string input = "\"aaa\" + 10 == test + 10";
            Parser parser = new Parser(input, new() { { "test", "aaa" } });
            var root = parser.Parse();
            var instructions = parser.Compile(root).Flow;
            Console.WriteLine($"Result: {Run(instructions)}");
        }
    }

    static object Run(List<object> flow)
    {
        Stack<object> stack = new();
        foreach (var item in flow)
        {
            if (item is Delegate func)
            {
                var parameters = func.Method.GetParameters();
                var paramsArray = parameters.Select(x => stack.Pop()).Reverse().ToArray();
                stack.Push(func.DynamicInvoke(paramsArray));
            }
            else
            {
                stack.Push(item);
            }
            Console.WriteLine($"{item} = {stack.Peek()}");
        }

        return stack.Pop();
    }
}
