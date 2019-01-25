using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChakraUWP.Attributes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChakraUWP.Tests
{
    [TestClass]
    public class EngineTests
    {
        private Engine engine;

        [TestInitialize]
        public void TestInitialize()
        {
            engine = new Engine();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            engine.Dispose();
        }

        [TestMethod]
        public void JSCreateClassTest()
        {
            engine.AddType("SimpleClass", typeof(SimpleClass));

            var result = engine.Execute("var i = new SimpleClass();\ni.Title = 'test1234';i;");
            var output = engine.FromJavaScriptValue<SimpleClass>(result);

            Assert.AreEqual("test1234", output.Title);
        }

        [TestMethod]
        public void JSCreateClassAndCallMethodTest()
        {
            engine.AddType("SimpleClass", typeof(SimpleClass));

            engine.Execute("var i = new SimpleClass();\ni.SayHi();");
        }

        [TestMethod]
        public void JSCreateClassAndCallActionMethodTest()
        {
            //     engine.AddType("SimpleClassTwo", typeof(SimpleClass));

            //      var result = engine.Execute("var addMethodClass = new SimpleClassTwo();\naddMethodClass.Add();\naddMethodClass.Number;");
            //     var output = engine.FromJavaScriptValue<int>(result);

            //    Assert.AreEqual(1, output);
        }

        [TestMethod]
        public void ConsoleLogMethodTest()
        {
            Console.WriteLine("Console Test (c#)");

            engine.AddType("Console", typeof(Console));

            engine.Execute("Console.WriteLine('Console Test(JS)', null)");
        }

        [TestMethod]
        public void JSCreateClassAndCallReturnMethodTest()
        {
            engine.AddType("SimpleClass", typeof(SimpleClass));

            var result = engine.Execute("var i = new SimpleClass();\ni.SayHiWithReturn();");
            var output = engine.FromJavaScriptValue<bool>(result);

            Assert.AreEqual(true, output);
        }

        [TestMethod]
        public void JSConvertIntTest()
        {
            var input = 5;

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<int>(js);

            Assert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertBoolTest()
        {
            var input = false;

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<bool>(js);

            Assert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertEnumTest()
        {
            var input = TestEnum.Test2;

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<TestEnum>(js);

            Assert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertStringTest()
        {
            var input = "Hello World";

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<string>(js);

            Assert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertDoubleTest()
        {
            var input = 5.6;

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<double>(js);

            Assert.AreEqual(input, output);
        }

        [TestMethod]
        public void JsConvertClassTest()
        {
            var input = new AdvancedObject();

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<AdvancedObject>(js);

            Assert.AreEqual(input.String, output.String);
        }

        [TestMethod]
        public void JSConvertArrayIntTest()
        {
            var input = new List<int> { 8, 3 };

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<List<int>>(js);

            CollectionAssert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertArrayBoolTest()
        {
            var input = new List<bool> { true, false };

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<List<bool>>(js);

            CollectionAssert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertArrayEnumTest()
        {
            var input = new List<TestEnum> { TestEnum.Test1, TestEnum.Test3 };

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<List<TestEnum>>(js);

            CollectionAssert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertArrayStringTest()
        {
            var input = new List<string> { "Hello", "World" };

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<List<string>>(js);

            CollectionAssert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSConvertArrayDoubleTest()
        {
            var input = new List<double> { 5.7, 9.3 };

            var js = engine.ToJavaScriptValue(input);
            var output = engine.FromJavaScriptValue<List<double>>(js);

            CollectionAssert.AreEqual(input, output);
        }

        [TestMethod]
        public void JSAccessInt()
        {
            engine.AddObject("jsAccessInt", 5);
            var output = engine.Evaluate<int>("jsAccessInt;");

            Assert.AreEqual(output, 5);
        }

        [TestMethod]
        public void JSAccessBool()
        {
            engine.AddObject("jsAccessBool", false);
            var output = engine.Evaluate<bool>("jsAccessBool;");

            Assert.AreEqual(output, false);
        }

        [TestMethod]
        public void JSAccessString()
        {
            engine.AddObject("jsAccessString", "Hello World");
            var output = engine.Evaluate<string>("jsAccessString;");

            Assert.AreEqual(output, "Hello World");
        }

        [TestMethod]
        public void JSAccessDouble()
        {
            engine.AddObject("jsAccessDouble", 5.3);
            var output = engine.Evaluate<double>("jsAccessDouble;");

            Assert.AreEqual(output, 5.3);
        }

        [TestMethod]
        public void JSAccessEnum()
        {
            engine.AddObject("jsAccessEnum", TestEnum.Test2);
            var output = engine.Evaluate<TestEnum>("jsAccessEnum;");

            Assert.AreEqual(output, TestEnum.Test2);
        }

        [TestMethod]
        public void JSAccessClass()
        {
            engine.AddObject("jsAccessClass", new SimpleClass() { Title = "Custom Title" });
            var output = engine.Evaluate<SimpleClass>("jsAccessClass;");

            Assert.AreEqual(output.Title, "Custom Title");
        }

        [TestMethod]
        public void JSAccessClassString()
        {
            engine.AddObject("jsAccessClassString", new AdvancedObject());
            var output = engine.Evaluate<string>("jsAccessClassString.String;");

            Assert.AreEqual(output, "hello world");
        }

        [TestMethod]
        public void JSCallAdvancedMethods()
        {
            engine.AddObject("advancedClass", new AdvancedClass());
            engine.AddType("holder", typeof(HolderClass));

            engine.Execute("var holder1 = new holder();");
            engine.Execute("holder1.Title = 'test123';");

            engine.Execute("var holder2 = new holder();");
            engine.Execute("holder2.Title = 'test456';");

            engine.Execute("var holder3 = new holder();");
            engine.Execute("holder3.Title = 'test789';");

            engine.Execute("var holders = [holder1, holder2, holder3]");

            engine.Execute("advancedClass.ArrayMethod(holders);");
        }

        [TestMethod]
        public void JsCallCallbackMethod()
        {
            engine.AddObject("advancedClass2", new AdvancedClass());
            engine.Execute("advancedClass2.FuncTest(function (i) {\nreturn i + 1;\n});");
        }
    }

    public enum TestEnum
    {
        Test1,
        Test2,
        Test3
    }

    [TestClass]
    public class AdvancedObject
    {
        [JavaScriptProperty("boolean")]
        public bool Boolean { get; } = false;

        [JavaScriptProperty("string")]
        public string String { get; } = "hello world";

        [JavaScriptProperty("int")]
        public int Int { get; } = 3;

        [JavaScriptProperty("double")]
        public double Double { get; } = 1.03;

        [JavaScriptProperty("advanced")]
        public AnotherAdvancedObject Advanced { get; } = new AnotherAdvancedObject();

        [JavaScriptMethod("simpleTest")]
        public void SimpleTest()
        { }

        [JavaScriptMethod("testAsync")]
        public async Task TestAsync()
        { }

        [JavaScriptMethod("simpleTestWithParam")]
        public void SimpleTestWithParam(string first, int second, double third, bool fourth)
        { }
    }

    [TestClass]
    public class AnotherAdvancedObject
    {
        [JavaScriptProperty("boolean")]
        public bool Boolean { get; } = false;

        [JavaScriptProperty("string")]
        public string String { get; } = "hello world";

        [JavaScriptProperty("int")]
        public int Int { get; } = 3;

        [JavaScriptProperty("double")]
        public double Double { get; } = 1.03;
    }

    [TestClass]
    public class AdvancedClass
    {
        public TimeSpan Time { get; } = TimeSpan.FromDays(40);

        public void FuncTest(Func<int, int> execute)
        {
            var result = execute(50);
            Assert.AreEqual(result, 51);
        }

        public void ArrayMethod(HolderClass[] holders)
        {
            Assert.IsNotNull(holders);
        }
    }

    [TestClass]
    public class HolderClass
    {
        public string Title { get; set; }

        public string Description { get; set; }
    }

    [TestClass]
    public class SimpleClass
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public int Number { get; set; }

        public void SayHi()
        {
        }

        public void Add()
        {
            // Number += 1;
        }

        public bool SayHiWithReturn()
        {
            return true;
        }
    }
}