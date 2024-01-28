
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
                Parser parser = new Parser("(Vector3.one * 2).y", new() { { "test", new Vector3(1, 2, 3) } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(2, ExpressionEvaluator.Run(instructions));
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("new Vector3(2, 2, 2).y * 2", new() { { "test", new Vector3(1, 2, 3) } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(4, ExpressionEvaluator.Run(instructions));
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("test.y", new() { { "test", new Vector3(1, 2, 3) } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(2, ExpressionEvaluator.Run(instructions));
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("new Vector3(1,test,3)", new() { { "test", 10 } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(new Vector3(1,10,3), ExpressionEvaluator.Run(instructions));
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("test", new() { { "test", 10 } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(10, ExpressionEvaluator.Run(instructions));
            }
            {
                int dummyI = 0;
                Parser parser = new Parser("test[10]", new() { { "test", Enumerable.Range(0, 30).ToArray() } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(10, ExpressionEvaluator.Run(instructions));
            }
            {
                int dummyI = 0;
                var array = Enumerable.Range(0, 30).ToArray();
                Parser parser = new Parser("test[test[10][5]]", new() { { "test", Enumerable.Repeat(array, 20).ToArray() } });
                var instructions = parser.Compile(new List<Token>() { parser.Parse() }, ref dummyI).Flow;
                Assert.AreEqual(array, ExpressionEvaluator.Run(instructions));
            }
            {
                Parser parser = new Parser("max(abc(10), abc(20))", new() { { "test", 10 } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(20, ExpressionEvaluator.Run(instructions));
            }
            {
                Parser parser = new Parser("test()", new() { { "test", new Func<int>(() => 100) } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(100, ExpressionEvaluator.Run(instructions));
            }
            {
                Parser parser = new Parser("(float)--2 / 3 + abc(50) + --test * max(10, 20 * 20) +20 + 2+3*4* -(5 + 6)", new() { { "test", 10 } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(3940, (int)(float)ExpressionEvaluator.Run(instructions));
            }
            {
                Parser parser = new Parser("(10.0 - -20) == 30 && (test * 10 == 100)", new() { { "test", 10 } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(true, ExpressionEvaluator.Run(instructions));
            }
            {
                Parser parser = new Parser("\"aaa\" + 10 == test + 10", new() { { "test", "aaa" } });
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(true, ExpressionEvaluator.Run(instructions));
            }
        }
    }
}