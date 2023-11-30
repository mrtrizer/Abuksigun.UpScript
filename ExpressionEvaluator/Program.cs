using System;
using System.Collections.Generic;
using System.Reflection;

public enum TokenType
{
    Block,
    Skip,
    Literal,
    Reference,
    Binary,
    Unary
}

public record Token(TokenType Type, object Value, int StartIndex, int Length, List<Token> Children)
{
    public TokenType Type { get; set; } = Type;
    public object Value { get; set; } = Value;
    public int Length { get; set; } = Length;
}

public class Parser
{
    readonly string input;
    int position = 0;
    public Stack<Token> Stack { get; } = new();

    public Parser(string input)
    {
        this.input = input;
    }

    Token AddToken(TokenType tokenType, object value, int StartIndex, int Length)
    {
        if (tokenType == TokenType.Skip)
            return null;
        var token = new Token(tokenType, value, StartIndex, Length, new());
        Stack.Peek().Children.Add(token);
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
    bool And(params Func<bool>[] args) => AndParsed(TokenType.Block, null, args);
    bool AndParsed(TokenType tokenType, Func<string, object> parse, params Func<bool>[] args)
    {
        Stack.Push(AddToken(TokenType.Block, null, position, 0));
        bool result = true;
        int start = position;
        foreach (var func in args)
        {
            if (!func())
            {
                position = start;
                result = false;
                break;
            }
        }

        var token = Stack.Pop();
        if (result)
        {
            token.Type = tokenType;
            token.Length = position - token.StartIndex;
            if (parse != null)
                token.Value = parse(input.Substring(token.StartIndex, token.Length));
            if (tokenType == TokenType.Block)
            {
                var parent = Stack.Peek();
                if (token.Children.Count <= 1)
                    parent.Children.Remove(token);
                if (token.Children.Count == 1)
                    parent.Children.Add(token.Children[0]);
            }
        }
        else
        {
            var parent = Stack.Peek();
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

    bool Range(char start, char end, TokenType tokenType = TokenType.Skip)
    {
        if (input[position] >= start && input[position] <= end)
        {
            AddToken(tokenType, null, position, 1);
            position++;
            return true;
        }
        return false;
    }

    bool Number => And(() => Range('0', '9'), () => ZeroOrMore(() => Range('0', '9')));
    bool NumberLiteral => AndParsed(TokenType.Literal, x => int.Parse(x), () => Number);
    bool Letter => Or(() => Range('a', 'z'), () => Range('A', 'Z'));
    bool Reference => AndParsed(TokenType.Reference, x => x, () => Letter, () => ZeroOrMore(() => Or(() => Letter, () => Range('0', '9'))));
    bool Expression => And(() => Term, () => ZeroOrMore(() => Or(() => Match("+", TokenType.Binary), () => Match("-", TokenType.Binary)), () => Term));
    bool Term => And(() => Factor, ()=> ZeroOrMore(() => Or(() => Match("*", TokenType.Binary), () => Match("/", TokenType.Binary)), () => Factor));
    bool Factor => And(() => Space(), () => Or(() => NumberLiteral, () => Reference, () => Match("(", TokenType.Skip) && And(() => Expression, () => Match(")", TokenType.Skip))) && Space());

    public Token Parse()
    {
        Stack.Push(new Token(TokenType.Block, null, 0, 0, new()));
        bool success = Expression;
        var root = Stack.Pop();
        if (!success)
        {
            while (root.Children.Count > 0)
                root = root.Children.Last();
            throw new Exception($"Unexpect token at: {root.StartIndex + root.Length}\n{input.Substring(0, root.StartIndex)}###");
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

    static Dictionary<string, Delegate[]> additionalOperators = new()
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
        { "op_Implicit", new Delegate[] { (int a) => (float)a, (float a) => (double)a, (char a) => (int)a } },
        { "op_Explicit", new Delegate[] { (float a) => (int)a, (double a) => (float)a, (int a) => (char)a } }
    };

    static Delegate FindMethod(string name, params Type[] arguments)
    {
        if (arguments.First().GetMethod(name, arguments) is {} methodInfo)
            return Delegate.CreateDelegate(typeof(Delegate), methodInfo);

        foreach (var func in additionalOperators[name])
        {
            var parameters = func.Method.GetParameters();
            if (parameters.Length == arguments.Length && parameters.Select(x => x.ParameterType).SequenceEqual(arguments))
                return func;
        }
        return null;
    }

    static IEnumerable<Delegate> FindImplicitConversions(Type type)
    {
        List<Delegate> conversions = new List<Delegate>();

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var method in methods)
        {
            if (method.Name == "op_Implicit" && method.ReturnType != type && method.GetParameters()[0].ParameterType == type)
                conversions.Add(Delegate.CreateDelegate(typeof(Delegate), method));
        }

        foreach (var func in additionalOperators["op_Implicit"])
        {
            var parameters = func.Method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == type)
                conversions.Add(func);
        }

        return conversions;
    }

    static object GetReferenceValue(string name)
    {
        return 10;
    }

    static Type GetReferenceType(string name)
    {
        return typeof(int);
    }

    static Type CompileRecursive(string input, Token token, List<object> flow)
    {
        try
        {
            if (token.Type == TokenType.Literal)
            {
                flow.Add(token.Value);
                return token.Value.GetType();
            }
            else if (token.Type == TokenType.Reference)
            {
                flow.Add(() => GetReferenceValue(token.Value as string));
                return GetReferenceType(token.Value as string);
            }
            else if (token.Type == TokenType.Block)
            {
                Type t1 = CompileRecursive(input, token.Children[0], flow);
                for (int i = 1; i < token.Children.Count; i++)
                {
                    var child = token.Children[i];
                    if (child.Type == TokenType.Binary || child.Type == TokenType.Unary)
                    {
                        if (child.Type == TokenType.Binary)
                        {
                            Type t2 = CompileRecursive(input, token.Children[i + 1], flow);

                            // TODO: Find implicit conversion if can't find exact match

                            var func = FindMethod(binaryOperators[input.Substring(child.StartIndex, child.Length)], t1, t2);
                            if (func == null)
                                throw new Exception($"Binary operator {input.Substring(child.StartIndex, child.Length)} not found for types {t1} and {t2}");

                            flow.Add(func);
                            t1 = func.Method.ReturnType;
                        }
                        else if (child.Type == TokenType.Unary)
                        {
                            var func = FindMethod(unaryOperators[input.Substring(child.StartIndex, child.Length)], t1);
                            if (func == null)
                                throw new Exception($"Unary operator {input.Substring(child.StartIndex, child.Length)} not found for type {t1}");

                            flow.Add(func);
                            t1 = func.Method.ReturnType;
                        }
                    }
                }
                return t1;
            }
        }
        catch
        {
            // Next line will throw exception
        }
        throw new Exception($"Invalid expression at: {token.StartIndex}\n{input.Substring(0, token.StartIndex)}###");
    }

    public static List<object> Compile(string input, Token root)
    {
        List<object> instructions = new();
        CompileRecursive(input, root, instructions);
        return instructions;
    }
}

public static class Program
{
    static void PrintToken(string input, Token token, int level = 0)
    {
        Console.WriteLine($"{new string(' ', level * 2)}{token.Type} {input.Substring(token.StartIndex, token.Length)}");
        foreach (var child in token.Children)
            PrintToken(input, child, level + 1);
    }

    public static void Main()
    {
        string input = "test * 20 +20 + 2+3*4*(5 + 6)";
        Parser parser = new Parser(input);
        var root = parser.Parse();
        PrintToken(input, root);
        var instructions = Parser.Compile(input, root);
        Console.WriteLine($"Result: {Run(instructions)}");
    }

    static object Run(List<object> flow)
    {
        Stack<object> stack = new();
        foreach (var item in flow)
        {
            if (item is Delegate func)
            {
                var parameters = func.Method.GetParameters();
                if (parameters.Length == 2)
                {
                    var b = stack.Pop();
                    var a = stack.Pop();
                    stack.Push(func.DynamicInvoke(a, b));
                }
                else if (parameters.Length == 1)
                {
                    var a = stack.Pop();
                    stack.Push(func.DynamicInvoke(a));
                }
                else if (parameters.Length == 0)
                {
                    stack.Push(func.DynamicInvoke());
                }
            }
            else
            {
                stack.Push(item);
            }
            Console.WriteLine(item);
        }

        return stack.Pop();
    }
}
