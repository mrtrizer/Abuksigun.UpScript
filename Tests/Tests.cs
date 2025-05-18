using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Abuksigun.UpScript.Tests
{
    public class Tests
    {
        #region Containers
        public class TestObject
        {
            public int field;
            public int Property { get; set; }
        }
        #endregion

        #region Tests
        [Test]
        public void Test()
        {
            var max = new Func<int, int, int>((a, b) => a > b ? a : b);
            var abs = new Func<int, int>(i => Mathf.Abs(i));

            Assert.IsTrue((bool)ExpressionEvaluator.Run(new Parser("10 < 20").Compile().Flow));
            Assert.IsFalse((bool)ExpressionEvaluator.Run(new Parser("10 > 20").Compile().Flow));
            Assert.IsTrue((bool)ExpressionEvaluator.Run(new Parser("10 <= 20").Compile().Flow));
            Assert.IsFalse((bool)ExpressionEvaluator.Run(new Parser("10 >= 20").Compile().Flow));
            {
                var testObject = new TestObject();
                var variables = new Dictionary<string, object> { ["test"] = testObject, ["testInt"] = 10 };
                var parser = new Parser("testInt = test.field = 10", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                var testValue = (int)ExpressionEvaluator.Run(instructions, variables);
                Assert.AreEqual(10, testValue);
                Assert.AreEqual(10, variables["testInt"]);
                Assert.AreEqual(10, testObject.field);
            }
            {
                var variables = new Dictionary<string, object> { ["test_1"] = new Vector3(1, 2, 3) };
                var parser = new Parser("(Vector3.one * 4).y / test_1.y", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(2, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = new Vector3(1, 2, 3) };
                var parser = new Parser("-new Vector3(2, 2, 2).y * - 2", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(4, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = new Vector3(1, 2, 3) };
                var parser = new Parser("-test.y", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(-2, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = 1 };
                var parser = new Parser("++test", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(2, ExpressionEvaluator.Run(instructions, variables));
                Assert.AreEqual(2, variables["test"]);
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = new Vector3(1, 2, 3) };
                var parser = new Parser("--test.y", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(1, ExpressionEvaluator.Run(instructions, variables));
                Assert.AreEqual(new Vector3(1, 1, 3), variables["test"]);
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = 10 };
                var parser = new Parser("new Vector3(1,test,3)", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(new Vector3(1, 10, 3), ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = 10 };
                var parser = new Parser("Mathf.CeilToInt(new Vector3(test,test,3).x).ToString(\"D4\")", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual("0010", ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = Enumerable.Range(0, 30).Select(x => x.ToString()).ToArray() };
                var parser = new Parser("test[10] = test[10] + test[11]", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual("1011", ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var twoDimensionalArray = new string[10, 10];
                for (int i = 0; i < 10; i++)
                    for (int j = 0; j < 10; j++)
                        twoDimensionalArray[j, i] = $"{i}{j}";
                var variables = new Dictionary<string, object> { ["test"] = twoDimensionalArray };
                var parser = new Parser("test[5, 3]", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual("53", ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = Enumerable.Range(0, 30).ToArray() };
                var parser = new Parser("test[(4 + 1) * 2]", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(10, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = Enumerable.Range(0, 30).ToDictionary(x => x.ToString(), x => x) };
                var parser = new Parser("test[\"10\"]", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(10, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var array = Enumerable.Range(0, 30).ToArray();
                var variables = new Dictionary<string, object> { ["test"] = Enumerable.Repeat(array, 20).ToArray() };
                var parser = new Parser("test[test[10][5]]", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(array, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> {
                    ["test"] = 10,
                    ["max"] = max,
                    ["abs"] = abs
                };
                var parser = new Parser("10 + max(abs(10), abs(20))", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(30, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = new Func<int>(() => 100) };
                var parser = new Parser("10 + test()", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(110, ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> {
                    ["test"] = 10,
                    ["max"] = max,
                    ["abs"] = abs
                };
                var parser = new Parser("(float)- -2 / 3 + abs(50) + - -test * max(10, 20 * 20) +20 + 2+3*4* -(5 + 6)", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.AreEqual(3940, (int)(float)ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = 10 };
                var parser = new Parser("(10.0 - -20) == 30 && (test * 10 == 100)", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.IsTrue((bool)ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var variables = new Dictionary<string, object> { ["test"] = "aaa" };
                var parser = new Parser("\"aaa\" + 10 == test + 10", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                Assert.IsTrue((bool)ExpressionEvaluator.Run(instructions, variables));
            }
            {
                var success = false;
                var variables = new Dictionary<string, object> { ["test"] = new Action(() => success = true) };
                var parser = new Parser("test() ", variables);
                var instructions = parser.Compile(parser.Parse()).Flow;
                ExpressionEvaluator.Run(instructions, variables);
                Assert.IsTrue(success);
            }
        }
        #endregion
    }
}
