
using NUnit.Framework;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

namespace Abuksigun.UpScript.Tests    
{
    public class Tests
    {
        [Test]
        public void Test()
        {
            {
                int dummyI = 0;
                Parser parser = new Parser("test", new() { { "test", 10 } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("test[10]", new() { { "test", Enumerable.Range(0, 30).ToArray() } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("test[test[10][5]]", new() { { "test", Enumerable.Repeat(Enumerable.Range(0, 30).ToArray(), 20).ToArray() } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                Parser parser = new Parser("max(abc(10), abc(20))", new() { { "test", 10 } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                Parser parser = new Parser("test()", new() { { "test", new Func<int>(() => 100) } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                Parser parser = new Parser("(float)--2 / 3 + abc(50) + --test * max(10, 20 * 20) +20 + 2+3*4* -(5 + 6)", new() { { "test", 10 } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                Parser parser = new Parser("(10.0 - -20) == 30 && (test * 10 == 100)", new() { { "test", 10 } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
            {
                Parser parser = new Parser("\"aaa\" + 10 == test + 10", new() { { "test", "aaa" } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Console.WriteLine($"Result: {ExpressionEvaluator.Run(instructions)}");
            }
        }
    }
}