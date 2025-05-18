using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Text;
using System.Runtime.CompilerServices;

using IntFunc = System.Func<int, int, int>;
using FloatFunc = System.Func<float, float, float>;
using DoubleFunc = System.Func<double, double, double>;
using LongFunc = System.Func<long, long, long>;
using CharFunc = System.Func<char, char, char>;
using StringFunc = System.Func<string, string, string>;
using BoolFunc = System.Func<bool, bool, bool>;

using IntBoolFunc = System.Func<int, int, bool>;
using DoubleBoolFunc = System.Func<double, double, bool>;
using FloatBoolFunc = System.Func<float, float, bool>;
using LongBoolFunc = System.Func<long, long, bool>;
using CharBoolFunc = System.Func<char, char, bool>;
using StringBoolFunc = System.Func<string, string, bool>;


namespace Abuksigun.UpScript
{
    public enum TokenType { Block, Skip, Literal, Reference, MemberReference, Binary, Unary, Increment, ExplicitConversion, Function, Constructor, Index, Setter }

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
            public ParserException(string message, Exception e = null) : base(message, e) { }
        }

        record Method(object Func, Type ReturnType);
        public record CompilationResult(Type Type, List<object> Flow);

        readonly string input;
        int position = 0;
        Stack<Token> stack = new();
        internal record RunDelegate(int ArgsN);
        internal record Object(Func<Dictionary<string, object>, object> Get, Func<Dictionary<string, object>, object, object> Set);
        internal record Property(Func<object, object> Get, Func<object, object, object> Set);
        internal record Indexer(Func<object, object[], object> Get, Func<object, object[], object, object> Set, int ArgsN);
        internal record SetOperator { }
        public Dictionary<string, object> Variables { get; } = new();

        public Parser(string input, Dictionary<string, object> variables = null)
        {
            this.input = input;
            Variables = variables ?? new();
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
            while (And(funcs)) { /* Nothing */ }
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
            var block = stack.Peek();
            int startTokensN = block.Children.Count;
            foreach (var func in args)
            {
                if (!func())
                {
                    position = start;
                    block.Children.RemoveRange(startTokensN, block.Children.Count - startTokensN);
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
        bool Character(char c) => input.Length > position && input[position] == c && IncPosition;
        bool NotQuote() => input.Length > position && (input[position] != '"' || (position > 0 && input[position - 1] == '\\')) && IncPosition;

        bool Digits => And(() => Range('0', '9'), () => ZeroOrMore(() => Range('0', '9')));
        bool Letter => Or(() => Range('a', 'z'), () => Range('A', 'Z'), () => Character('_'));
        bool Integer => Block(() => Digits, TokenType.Literal, x => int.Parse(x));
        bool Float => Block(() => And(() => Digits, () => Match("."), () => Digits), TokenType.Literal, x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
        bool NumberLiteral => Or(() => Float, () => Integer);
        bool BoolLiteral => Block(() => Or(() => Match("true"), () => Match("false")), TokenType.Literal, x => bool.Parse(x));
        bool StringLiteral => Block(() => And(() => Match("\""), () => ZeroOrMore(() => NotQuote()), () => Match("\"")), TokenType.Literal, x => x[1..^1]);
        bool Identifier => Block(() => And(() => Letter, () => ZeroOrMore(() => Or(() => Letter, () => Range('0', '9')))));

        bool Constructor => Block(() => And(() => Match("new"), () => Space(), () => Reference, () => Space(), () => FunctionArguments), TokenType.Constructor);
        bool FunctionArguments => Block(() => And(() => Match("("), () => Or(() => Match(")"), () => And(() => ZeroOrMore(() => Expression, () => Match(",")), () => Expression, () => Match(")"))), () => Space()), TokenType.Function);
        bool Index => Block(() => And(() => Match("["), () => Or(() => Match("]"), () => And(() => ZeroOrMore(() => Expression, () => Match(",")), () => Expression, () => Match("]"))), () => Space()), TokenType.Index); // Block(() => And(() => Match("["), () => Expression, () => Match("]")), TokenType.Index);

        bool ExplicitConversion => Block(() => And(() => Block(() => And(() => Match("("), () => Space(), () => Identifier, () => Space(), () => Match(")")), TokenType.ExplicitConversion, (x) => x[1..^1].Trim()), () => Factor));
        bool Reference => Block(() => Identifier, TokenType.Reference, x => x);
        bool MemberReference => Block(() => And(() => Match("."), () => Identifier), TokenType.MemberReference, x => x.Trim('.'));
        bool BracketBlock => Block(() => And(() => Match("("), () => Expression, () => Match(")")));
        bool Factor => Block(() => And(() => Space(), () => Or(() => BlockValue, () => Unary), () => Space()));

        bool BlockValue => Block(() => And(() => Or(() => ExplicitConversion, () => NumberLiteral, () => StringLiteral, () => BoolLiteral, () => Constructor, () => Reference, () => BracketBlock), () => ZeroOrMore(() => Or(() => MemberReference, () => FunctionArguments, () => Index))));

        bool Unary => Block(() => And(() => Or(() => Match("++", TokenType.Increment), () => Match("--", TokenType.Increment), () => Match("-", TokenType.Unary), () => Match("!", TokenType.Unary)), () => Space(), () => Or(() => BlockValue, () => Unary)));
        bool Term => Block(() => And(() => Factor, () => ZeroOrMore(() => Or(() => Match("*", TokenType.Binary), () => Match("/", TokenType.Binary), () => Match("%", TokenType.Binary)), () => Factor)));
        bool Additive => Block(() => And(() => Term, () => ZeroOrMore(() => Or(() => Match("+", TokenType.Binary), () => Match("-", TokenType.Binary)), () => Term)));
        bool Comparison => Block(() => And(() => Additive, () => ZeroOrMore(() => Or(() => Match("<=", TokenType.Binary), () => Match(">=", TokenType.Binary), () => Match("<", TokenType.Binary), () => Match(">", TokenType.Binary), () => Match("==", TokenType.Binary), () => Match("!=", TokenType.Binary)), () => Additive)));
        bool RSExpression => Block(() => And(() => Comparison, () => ZeroOrMore(() => Or(() => Match("&&", TokenType.Binary), () => Match("||", TokenType.Binary)), () => Comparison)));
        bool LSExpression => Block(() => And(() => Reference, () => ZeroOrMore(() => Or(() => MemberReference, () => Index))));
        bool Expression => Block(() => Or(() => And(() => LSExpression, () => Space(), () => Match("=", TokenType.Setter), () => Space(), () => Expression), () => RSExpression));

        static string TokenToString(string input, Token token, int level = 0)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{new string(' ', level * 2)}{token.Type} {input.Substring(token.StartIndex, token.Length)}");
            foreach (var child in token.Children)
                stringBuilder.Append(TokenToString(input, child, level + 1));
            return stringBuilder.ToString();
        }

        public Token Parse()
        {
            stack.Push(new Token(TokenType.Block, null, 0, 0, new()));
            bool success = Expression;
            var root = stack.Pop().Children[0];
            if (!success || root.Length != input.Length)
            {
                string currentTokensString = TokenToString(input, root);
                while (root.Children.Count > 0)
                    root = root.Children[^1];
                throw new ParserException($"Unexpect token at: {root.StartIndex + root.Length}\n{input.Substring(0, root.StartIndex)}###\n{currentTokensString}");
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
            { "++", "op_Increment" },
            { "--", "op_Decrement" }
        };

        static Dictionary<string, Delegate[]> fastOperators = new()
        {
            { "op_Addition", new Delegate[] { new IntFunc((a, b) => a + b), new DoubleFunc((a, b) => a + b), new FloatFunc((a, b) => a + b), new LongFunc((a, b) => a + b), new StringFunc((a, b) => a + b), new Func<char, char, string>((a, b) => a.ToString() + b.ToString()) } },
            { "op_Subtraction", new Delegate[] { new IntFunc((a, b) => a - b), new DoubleFunc((a, b) => a - b), new FloatFunc((a, b) => a - b), new LongFunc((a, b) => a - b) } },
            { "op_Multiply", new Delegate[] { new IntFunc((a, b) => a * b), new DoubleFunc((a, b) => a * b), new FloatFunc((a, b) => a * b), new LongFunc((a, b) => a * b) } },
            { "op_Division", new Delegate[] { new IntFunc((a, b) => a / b), new DoubleFunc((a, b) => a / b), new FloatFunc((a, b) => a / b), new LongFunc((a, b) => a / b) } },
            { "op_Modulus", new Delegate[] { new IntFunc((a, b) => a % b), new DoubleFunc((a, b) => a % b), new FloatFunc((a, b) => a % b), new LongFunc((a, b) => a % b) } },
            { "op_LessThan", new Delegate[] { new IntBoolFunc((a, b) => a < b), new DoubleBoolFunc((a, b) => a < b), new FloatBoolFunc((a, b) => a < b), new LongBoolFunc((a, b) => a < b) } },
            { "op_LessThanOrEqual", new Delegate[] { new IntBoolFunc((a, b) => a <= b), new DoubleBoolFunc((a, b) => a <= b), new FloatBoolFunc((a, b) => a <= b), new LongBoolFunc((a, b) => a <= b) } },
            { "op_GreaterThan", new Delegate[] { new IntBoolFunc((a, b) => a > b), new DoubleBoolFunc((a, b) => a > b), new FloatBoolFunc((a, b) => a > b), new LongBoolFunc((a, b) => a > b) } },
            { "op_GreaterThanOrEqual", new Delegate[] { new IntBoolFunc((a, b) => a >= b), new DoubleBoolFunc((a, b) => a >= b), new FloatBoolFunc((a, b) => a >= b), new LongBoolFunc((a, b) => a >= b) } },
            { "op_Equality", new Delegate[] { new IntBoolFunc((a, b) => a == b), new DoubleBoolFunc((a, b) => a == b), new FloatBoolFunc((a, b) => a == b), new LongBoolFunc((a, b) => a == b), new BoolFunc((a, b) => a == b), new StringBoolFunc((a, b) => a == b), new CharBoolFunc((a, b) => a == b) } },
            { "op_Inequality", new Delegate[] { new IntBoolFunc((a, b) => a != b), new DoubleBoolFunc((a, b) => a != b), new FloatBoolFunc((a, b) => a != b), new LongBoolFunc((a, b) => a != b), new BoolFunc((a, b) => a != b), new StringBoolFunc((a, b) => a != b), new CharBoolFunc((a, b) => a != b) } },
            { "op_BitwiseAnd", new Delegate[] { new IntFunc((a, b) => a & b), new LongFunc((a, b) => a & b), new BoolFunc((a, b) => a && b) } },
            { "op_BitwiseOr", new Delegate[] { new IntFunc((a, b) => a | b), new LongFunc((a, b) => a | b), new BoolFunc((a, b) => a || b) } },
            { "op_UnaryNegation", new Delegate[] { new Func<int, int>(a => -a), new Func<double, double>(a => -a), new Func<float, float>(a => -a), new Func<long, long>(a => -a) } },
            { "op_LogicalNot", new Delegate[] { new Func<bool, bool>(a => !a) } },
            { "op_Increment", new Delegate[] { new Func<int, int>(a => a + 1), new Func<float, float>(a => a + 1), new Func<double, double>(a => a + 1), new Func<long, long>(a => a + 1) } },
            { "op_Decrement", new Delegate[] { new Func<int, int>(a => a - 1), new Func<float, float>(a => a - 1), new Func<double, double>(a => a - 1), new Func<long, long>(a => a - 1) } },
            { "op_Implicit", new Delegate[] { (Func<int, float>)(a => a), (Func<float, double>)(a => a), (Func<char, int>)(a => a), (Func<int, string>)(a => a.ToString()), (Func<float, string>)(a => a.ToString()), (Func<double, string>)(a => a.ToString()), (Func<bool, string>)(a => a.ToString()) } },
            { "op_Explicit", new Delegate[] { (Func<float, int>)(a => (int)a), (Func<double, float>)(a => (float)a), (Func<int, char>)(a => (char)a) } },
        };

        static Dictionary<string, Type> typesMap = new()
        {
            { "int", typeof(int) },
            { "float", typeof(float) },
            { "double", typeof(double) },
            { "long", typeof(long) },
            { "bool", typeof(bool) },
            { "string", typeof(string) },
            { "char", typeof(char) },
            { "Vector3", typeof(Vector3) },
            { "Mathf", typeof(Mathf) },
        };

        Method FindMethod(string name, params Type[] arguments)
        {
            if (name.StartsWith("op_"))
            {
                foreach (var func in fastOperators[name])
                {
                    var parameters = func.Method.GetParameters();
                    if (parameters.Length == arguments.Length && parameters.Select(x => x.ParameterType).SequenceEqual(arguments))
                        return new(func, func.Method.ReturnType);
                }
                if (arguments[0].GetMethod(name, arguments) is { } method)
                    return new(method, method.ReturnType);
            }
            else if (Variables.TryGetValue(name, out var variable) && variable is Delegate func)
            {
                var parameters = func.Method.GetParameters();
                if (parameters.Length == arguments.Length && parameters.Select(x => x.ParameterType).SequenceEqual(arguments))
                    return new(func, func.Method.ReturnType);
            }
            return null;
        }

        static List<Method> FindConversions(Type type, string name)
        {
            List<Method> conversions = new List<Method>();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == name && method.ReturnType != type && method.GetParameters()[0].ParameterType == type)
                    conversions.Add(new(new Action<object>((object a) => method.Invoke(null, new[] { a })), method.ReturnType));
            }
            foreach (var func in fastOperators[name])
            {
                var parameters = func.Method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == type)
                    conversions.Add(new(func, func.Method.ReturnType));
            }
            return conversions;
        }

        CompilationResult AddMethod(string name, params CompilationResult[] arguments)
        {
            List<object> flow = new();
            var func =  FindMethod(name, arguments.Select(x => x.Type).ToArray());
            if (func == null)
            {
                var allConversions = arguments.Select(x => FindConversions(x.Type, "op_Implicit")).ToList();
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
            foreach (var argument in allConversions[argIndex].Prepend(null))
            {
                current[argIndex] = argument;
                if (argIndex == allConversions.Count - 1)
                    combinations.Add(current.ToArray());
                else
                    GenerateCombinations(allConversions, current, combinations, argIndex + 1);
            }
        }

        static MethodInfo[] GetExtensionMethods(Type type, string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                .Where(x => x.IsSealed && !x.IsGenericType && !x.IsNested)
                .SelectMany(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(x => x.Name == name && x.IsDefined(typeof(ExtensionAttribute), false) && x.GetParameters()[0].ParameterType == type))
                .ToArray();
        }

        static object SetValue(object obj, object value, PropertyInfo propertyInfo, FieldInfo fieldInfo)
        {
            if (propertyInfo != null)
                propertyInfo.SetValue(obj, value);
            else if (fieldInfo != null)
                fieldInfo.SetValue(obj, value);
            return value;
        }

        public CompilationResult Compile(Token token = null)
        {
            int dummyI = 0;
            List<Token> tmpList = new List<Token> { token ?? Parse() };
            return Compile(tmpList, ref dummyI);
        }

        public CompilationResult Compile(List<Token> tokens, ref int parentI)
        {
            var token = tokens[parentI];
            try
            {
                CompilationResult result = null;
                int i = 0;
                if (token.Type == TokenType.Literal)
                {
                    result = new(token.Value.GetType(), new List<object>() { token.Value });
                }
                else if (token.Type == TokenType.Reference)
                {
                    if (tokens.Count - 1 > parentI && tokens[parentI + 1].Type == TokenType.Function)
                        result = AddMethod(token.Value as string, tokens[++parentI].Children.Select(Compile).ToArray());
                    else if (Variables.TryGetValue(token.Value as string, out var variable))
                        result = new(variable.GetType(), new() { new Object((Dictionary<string, object> variables) => variables[token.Value as string], (Dictionary<string, object> variables, object value) => (variables[token.Value as string] = value)) });
                    else if (typesMap.TryGetValue(token.Value as string, out var type))
                        result = new(type, new() { type });
                    else
                        throw new ParserException($"Type or variable {token.Value as string} not found");
                }
                else if (token.Type == TokenType.Block)
                {
                    var child = token.Children[0];
                    
                    if (child.Type == TokenType.ExplicitConversion)
                    {
                        i = 1;
                        CompilationResult r1 = Compile(token.Children, ref i);
                        var type = typesMap[child.Value as string];
                        var method = FindConversions(r1.Type, $"op_Explicit").Find(x => x.ReturnType == type);
                        method ??= FindConversions(r1.Type, $"op_Implicit").Find(x => x.ReturnType == type);
                        if (method == null)
                            throw new ParserException($"There is no explicit conversion for type {r1.Type.Name} into type {child.Value as string}");
                        result = new(method.ReturnType, r1.Flow.Append(method.Func).ToList());
                    }
                    else if (child.Type == TokenType.Unary)
                    {
                        i = 1;
                        CompilationResult r1 = Compile(token.Children, ref i);
                        result = AddMethod(unaryOperators[input.Substring(child.StartIndex, child.Length)], r1);
                    }
                    else if (child.Type == TokenType.Increment)
                    {
                        i = 1;
                        CompilationResult r1 = Compile(token.Children, ref i);
                        string op = input.Substring(child.StartIndex, child.Length);
                        if (r1.Flow[^1] is not (Property or Object))
                            throw new ParserException($"{op} operator can only be used on left side expressions such as variable");
                        if (!r1.Type.IsPrimitive)
                            throw new ParserException($"{op} operator can only be used on primitive types");
                        var increment = FindMethod(unaryOperators[op], r1.Type).Func;
                        result = new CompilationResult(r1.Type, r1.Flow.Concat(r1.Flow).Append(increment).Append(new SetOperator()).ToList());
                    }
                    else
                    {
                        CompilationResult r1 = Compile(token.Children, ref i);
                        for (; i < token.Children.Count; i++)
                        {
                            child = token.Children[i];
                            if (child.Type == TokenType.Binary)
                            {
                                i++;
                                CompilationResult r2 = Compile(token.Children, ref i);
                                r1 = AddMethod(binaryOperators[input.Substring(child.StartIndex, child.Length)], r1, r2);
                            }
                            else if (child.Type == TokenType.Setter)
                            {
                                i++;
                                CompilationResult r2 = Compile(token.Children, ref i);
                                r1 = new CompilationResult(r1.Type, r1.Flow.Concat(r2.Flow).Append(new SetOperator()).ToList());
                            }
                            else if (child.Type == TokenType.MemberReference)
                            {
                                var memberName = child.Value as string;
                                bool staticMember = r1.Flow.Last() is Type;
                                var members = r1.Type.GetMember(memberName, BindingFlags.Public | (staticMember ? BindingFlags.Static : BindingFlags.Instance));
                                var methods = members.Where(x => x is MethodInfo).Cast<MethodInfo>().ToList();
                                if (methods.Any())
                                {
                                    if (!staticMember)
                                        methods.AddRange(GetExtensionMethods(r1.Type, memberName));
                                    var arguments = token.Children[++i].Children.Select(Compile);
                                    var argumentTypes = arguments.Select(x => x.Type).ToArray();
                                    var method = methods.Find(x => x.GetParameters().Select(x => x.ParameterType).SequenceEqual(argumentTypes));
                                    if (method == null)
                                        throw new ParserException($"Method or extension {memberName} not found for types {string.Join(", ", argumentTypes.Select(x => x.Name))}");
                                    if (method.ReturnType == typeof(void))
                                        throw new ParserException($"Method {memberName} returns void. Void methods are not supported, use functional approach.");
                                    r1 = new CompilationResult(method.ReturnType, r1.Flow.Concat(arguments.SelectMany(x => x.Flow)).Append(method).ToList());
                                }
                                else
                                {
                                    var propertyInfo = members.FirstOrDefault() as PropertyInfo;
                                    var fieldInfo = members.FirstOrDefault() as FieldInfo;
                                    var memberReturnType = propertyInfo?.PropertyType ?? fieldInfo?.FieldType;
                                    r1 = new CompilationResult(memberReturnType, r1.Flow.Append(new Property((object obj) => propertyInfo?.GetValue(obj) ?? fieldInfo?.GetValue(obj), (object obj, object value) => SetValue(obj, value, propertyInfo, fieldInfo))).ToList());
                                }
                            }
                        }
                        result = r1;
                    }
                }
                else if (token.Type == TokenType.Constructor)
                {
                    var typeName = token.Children[0].Value as string;
                    var type = typesMap[typeName];
                    var args = token.Children[1].Children.Select(Compile).ToArray();
                    result = new CompilationResult(type, args.SelectMany(x => x.Flow).Append(type.GetConstructor(args.Select(x => x.Type).ToArray())).ToList());
                }

                while (tokens.Count - 1 > parentI && tokens[parentI + 1].Type is (TokenType.Index or TokenType.Function))
                {
                    var nextTokenType = tokens[parentI + 1].Type;
                    var args = tokens[++parentI].Children.Select(Compile).Prepend(result).ToArray();

                    if (nextTokenType == TokenType.Index)
                    {
                        var type = args[0].Type;
                        var indexTypes = args.Skip(1).Select(x => x.Type).ToArray();
                        var elementType = type.IsArray ? type.GetElementType() : type.GetProperty("Item").PropertyType;
                        var getter = type.IsArray ? type.GetMethod("GetValue", indexTypes) : type.GetProperty("Item").GetGetMethod();
                        var setter = type.IsArray ? type.GetMethod("SetValue", indexTypes.Prepend(elementType).ToArray()) : type.GetProperty("Item").GetSetMethod();
                        result = new CompilationResult(elementType, args.SelectMany(x => x.Flow).Append(new Indexer(
                            (object subject, object[] indices) => getter.Invoke(subject, indices), 
                            (object subject, object[] indices, object value) => {
                                setter.Invoke(subject, indices.Prepend(value).ToArray());
                                return value;
                            }, 
                            args.Length - 1)).ToList());
                    }
                    else if (nextTokenType == TokenType.Function)
                    {
                        result = new CompilationResult(typeof(Delegate), args.SelectMany(x => x.Flow).Append(new RunDelegate(args.Length - 1)).ToList());
                    }
                }

                if (result != null)
                    return result;

                throw new ParserException($"Unexpected token type {token.Type}");
            }
            catch (Exception e)
            {
                throw;
                //throw new ParserException($"Invalid expression at: {token.StartIndex}\n{input.Substring(0, token.StartIndex)}###", e);
            }
        }
    }

    public static class ExpressionEvaluator
    {
        public class EvaluatorException : Exception
        {
            public EvaluatorException(string message, Exception e = null) : base(message, e) { }
        }

        public static object Run(List<object> flow, Dictionary<string, object> variables = null)
        {
            Stack<object> stack = new();

            object GetValue(object input)
            {
                if (input is Parser.Object @object)
                    return @object.Get(variables);
                if (input is Parser.Property property)
                    return property.Get(GetValue(stack.Pop()));
                if (input is Parser.Indexer indexer)
                {
                    var arguments = Enumerable.Range(0, indexer.ArgsN).Select(x => GetValue(stack.Pop())).ToArray();
                    return indexer.Get(GetValue(stack.Pop()), arguments);
                }
                return input;
            }

            foreach (var item in flow)
            {
                if (item is Delegate func)
                {
                    var paramsArray = func.Method.GetParameters().Select(x => stack.Pop()).Select(GetValue).Reverse().ToArray();
                    stack.Push(func.DynamicInvoke(paramsArray));
                }
                else if (item is MethodInfo methodInfo)
                {
                    var paramsArray = methodInfo.GetParameters().Select(x => stack.Pop()).Select(GetValue).Reverse().ToArray();
                    if (methodInfo.IsStatic)
                        stack.Push(methodInfo.Invoke(null, paramsArray));
                    else
                        stack.Push(methodInfo.Invoke(GetValue(stack.Pop()), paramsArray));
                }
                else if (item is Parser.RunDelegate runDelegate)
                {
                    func = stack.Pop() as Delegate;
                    var paramsArray = Enumerable.Range(0, runDelegate.ArgsN).Select(x => stack.Pop()).Select(GetValue).ToArray();
                    stack.Push(func.DynamicInvoke(paramsArray));
                }
                else if (item is ConstructorInfo constructor)
                {
                    var paramsArray = Enumerable.Range(0, constructor.GetParameters().Length).Select(x => stack.Pop()).Select(GetValue).Reverse().ToArray();
                    stack.Push(constructor.Invoke(paramsArray));
                }
                else if (item is Parser.SetOperator setOperator)
                {
                    var rightSide = GetValue(stack.Pop());
                    var leftSide = stack.Pop();
                    if (leftSide is Parser.Property property)
                    {
                        stack.Push(property.Set(GetValue(stack.Pop()), rightSide));
                    }
                    else if (leftSide is Parser.Object @object)
                    {
                        stack.Push(@object.Set(variables, rightSide));
                    }
                    else if (leftSide is Parser.Indexer indexer)
                    {
                        var arguments = Enumerable.Range(0, indexer.ArgsN).Select(x => GetValue(stack.Pop())).ToArray();
                        stack.Push(indexer.Set(GetValue(stack.Pop()), arguments, rightSide));
                    }
                    else
                    {
                        throw new EvaluatorException($"Invalid left side of assignment: {leftSide}");
                    }
                }
                else
                {
                    stack.Push(item);
                }
                Console.WriteLine($"{item} = {stack.Peek()}");
            }

            return GetValue(stack.Pop());
        }
    }
}