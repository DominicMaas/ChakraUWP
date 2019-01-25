# ChakraUWP
UWP C# Bindings for the ChakraCore JavaScript engine. Mostly working, some tests done, but other things still to do.

Converting from C# to JavaScript requires a few tweaks
Converting from JavaScript to c# requires a lot more work.

Betters docs to come.

## Usage

### Basic

```
using (var engine = new Engine())
{
    engine.Execute("var k = 4;");
    var result = engine.Evaluate<int>("k");
}
```

### Add Object

```

class MyClass
{
    public string MyString { get; } = "Hello World";
}

...

using (var engine = new Engine())
{
    engine.AddObject("myObject", new MyClass());
    var result = engine.Evaluate<string>("myObject.MyString");
    // result == "Hello World"
}

```

### Add Type

```

class MyClass
{ }

...

using (var engine = new Engine())
{
    engine.AddType("MyClass", typeof(MyClass));
    engine.Execute("var myObject = new MyClass();");
    var result = engine.Evaluate<MyClass>("myObject");
    // result == instance of MyClass.
}

```
