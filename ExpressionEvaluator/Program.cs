using System;
using System.Collections.Generic;

public enum TokenType
{
    Block,
    Skip,
    ValueToken,
    Plus,
    Minus,
    Multiply,
    Divide
}

public record Token(TokenType Type, int StartIndex, int Length, List<Token> Children);

public class Parser
{
    readonly string input;
    int position = 0;
    public Stack<Token> Stack { get; } = new();

    public Parser(string input)
    {
        this.input = input;
    }

    Token AddToken(TokenType tokenType, int StartIndex, int Length)
    {
        if (tokenType == TokenType.Skip)
            return null;
        var token = new Token(tokenType, StartIndex, Length, new());
        Stack.Peek().Children.Add(token);
        return token;
    }
    bool PushToken(TokenType tokenType)
    {
        Stack.Push(AddToken(tokenType, position, 0));
        return true;
    }
    bool PopToken() 
    {
        return Stack.Pop() != null;
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
    bool Or(params bool[] args) => Array.IndexOf(args, true) != -1;
    bool Match(string str, TokenType tokenType)
    {
        if (position + str.Length - 1 < input.Length && input.AsSpan(position, str.Length).SequenceEqual(str.AsSpan()))
        {
            AddToken(tokenType, position, str.Length);
            position += str.Length;
            return true;
        }
        return false;
    }

    bool Number()
    {
        int start = position;
        while (position < input.Length && char.IsDigit(input[position]))
            position++;
        if (position > start)
            return AddToken(TokenType.ValueToken, start, position - start) != null;
        return false;
    }
    bool Expression() => PushToken(TokenType.Block) && Term() && ZeroOrMore(() => Or(Match("+", TokenType.Plus), Match("-", TokenType.Minus)) && Term()) && PopToken();
    bool Term() => PushToken(TokenType.Block) && Factor() && ZeroOrMore(() => Or(Match("*", TokenType.Multiply), Match("/", TokenType.Divide)) && Factor()) && PopToken();
    bool Factor() => PushToken(TokenType.Block) && Space() && Or(Number(), Match("(", TokenType.Skip) && PushToken(TokenType.Block) && Expression() && Match(")", TokenType.Skip) && PopToken()) && Space() && PopToken();

    Token CleanUp(Token token) => token.Children.Count == 1
            ? CleanUp(token.Children[0])
            : new Token(token.Type, token.StartIndex, token.Length, token.Children.Select(CleanUp).ToList());

    public Token Parse()
    {
        Stack.Push(new Token(TokenType.Block, 0, 0, new()));
        Expression();
        return CleanUp(Stack.Pop());
    }

    static int Plus(object o1, object o2) => (int)o1 + (int)o2;
    static int Minus(object o1, object o2) => (int)o1 - (int)o2;
    static int Multiply(object o1, object o2) => (int)o1 * (int)o2;
    static int Divide(object o1, object o2) => (int)o1 / (int)o2;

    public static object Compile(string input, Token token, List<object> flow)
    {
        if (token.Type == TokenType.ValueToken)
        {
            int value = int.Parse(input.Substring(token.StartIndex, token.Length));
            return value;
        }
        for (int i = 0; i < token.Children.Count; i++)
        {
            var child = token.Children[i];
            if (child.Type == TokenType.Plus || child.Type == TokenType.Minus || child.Type == TokenType.Multiply || child.Type == TokenType.Divide)
            {
                object o1 = Compile(input, token.Children[i - 1], flow);
                Type t1 = o1 is Delegate d1 ? d1.Method.ReturnType : o1.GetType();
                object o2 = Compile(input, token.Children[i + 1], flow);
                Type t2 = o2 is Delegate d2 ? d2.Method.ReturnType : o2.GetType();

                

                object method = child.Type switch { TokenType.Plus => Plus, TokenType.Minus => Minus, TokenType.Multiply => Multiply, TokenType.Divide => Divide, _ => throw new Exception("Fail") };
                flow.Add(method);
                flow.Add(o1);
                flow.Add(o2);
                return method;
            }
        }
        return null;
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
        string input = "10* 20 +20  +2+3*4*(5 + 6)";
        Parser parser = new Parser(input);
        var root = parser.Parse();
        PrintToken(input, root);
        List<object> flow = new();
        Parser.Compile(input, root, flow);
    }
}
