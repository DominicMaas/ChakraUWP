using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoundByte.Engine.Attributes;
using SoundByte.Engine.Hosting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SoundByte.Engine
{
    /// <summary>
    ///     Create a new JavaScript engine for the application
    /// </summary>
    public sealed class Engine : IDisposable
    {
        private static JavaScriptSourceContext _currentSourceContext = JavaScriptSourceContext.FromIntPtr(IntPtr.Zero);

        private static JavaScriptRuntime _runtime;

        private static readonly Queue _taskQueue = new Queue();

        private static readonly JavaScriptPromiseContinuationCallback _promiseContinuationDelegate = (task, _) =>
        {
            _taskQueue.Enqueue(task);
            task.AddRef();
        };

        public EngineOptions Options { get; }

        public Engine() : this(null)
        { }

        public Engine(Action<EngineOptions> options)
        {
            // Setup the options
            Options = new EngineOptions();
            options?.Invoke(Options);

            // Create the runtime
            var runtimeResult = Native.JsCreateRuntime(JavaScriptRuntimeAttributes.None, null, out _runtime);
            if (runtimeResult != JavaScriptErrorCode.NoError)
                throw new Exception($"Failed to create runtime. Code: {runtimeResult}");

            // Create the context
            var contextResult = Native.JsCreateContext(_runtime, out JavaScriptContext context);
            if (contextResult != JavaScriptErrorCode.NoError)
                throw new Exception($"Failed to create execution context. Code: {contextResult}");

            // Set the context
            var currentContextResult = Native.JsSetCurrentContext(context);
            if (currentContextResult != JavaScriptErrorCode.NoError)
                throw new Exception($"Failed to set current context. Code: {currentContextResult}");

            // Setup support for ES6 promises
            var callbackResult = Native.JsSetPromiseContinuationCallback(_promiseContinuationDelegate, IntPtr.Zero);
            if (callbackResult != JavaScriptErrorCode.NoError)
                throw new Exception($"Failed to setup callback for ES6 Promise. Code: {callbackResult}");

            // Project windows namespace (temp)
            if (Native.JsProjectWinRTNamespace("Windows") != JavaScriptErrorCode.NoError)
                throw new Exception("Failed to project windows namespace.");

            // Start debugging (temp)
            if (Native.JsStartDebugging() != JavaScriptErrorCode.NoError)
                throw new Exception("Failed to start debugging.");
        }

        #region Add Object

        public void AddObject(string name, object value)
        {
            // Get the global object
            var globalObject = JavaScriptValue.GlobalObject;
            AddObject(globalObject, name, value);
        }

        public void AddObject(JavaScriptValue parent, string name, object value)
        {
            // Convert to a JS property
            var jsValue = ToJavaScriptValue(value);

            // Set the property
            var childPropertyId = JavaScriptPropertyId.FromString(name);
            parent.SetProperty(childPropertyId, jsValue, true);
        }

        #endregion Add Object

        #region Add Type

        /// <summary>
        ///     Adds a type to the JavaScript engine so static methods can be called or
        ///     classes can be created based on this type.
        /// </summary>
        /// <param name="name">The name of the type.</param>
        /// <param name="type">The type to expose.</param>
        public void AddType(string name, Type type)
        {
            // Get the global object and call the add type method
            var globalObject = JavaScriptValue.GlobalObject;
            AddType(globalObject, name, type);
        }

        public void AddType(JavaScriptValue parent, string name, Type type)
        {
            // Create the function that will call the class constructor
            var constructor = JavaScriptValue.CreateFunction(new JavaScriptNativeFunction((callee, isConstructCall, arguments, argumentCount, callbackData) =>
            {
                return ToJavaScriptValue(Activator.CreateInstance(type));
            }));

            // Add static functions (e.g Console.WriteLine)
            if (type.IsClass && type != typeof(string))
            {
                // Get the properties and methods
                var methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static)
                    .Where(m => !m.IsSpecialName && m.IsPrivate == false).ToList();

                // Loop through all methods
                foreach (var method in methods)
                {
                    SetMethod(constructor, method, null);
                }
            }

            // Set the property
            var childPropertyId = JavaScriptPropertyId.FromString(name);
            parent.SetProperty(childPropertyId, constructor, true);
        }

        #endregion Add Type

        public T Evaluate<T>(string script)
        {
            var result = Execute(script);
            return FromJavaScriptValue<T>(result);
        }

        public JavaScriptValue Execute(string script)
        {
            // Run the script
            if (Native.JsRunScript(script, _currentSourceContext++, "", out var result) != JavaScriptErrorCode.NoError)
            {
                // Get error message and clear exception
                if (Native.JsGetAndClearException(out var exception) != JavaScriptErrorCode.NoError)
                    throw new Exception("Failed to get and clear exception");

                if (Native.JsGetPropertyIdFromName("message", out var messageName) != JavaScriptErrorCode.NoError)
                    throw new Exception("Failed to get error message id");

                if (Native.JsGetProperty(exception, messageName, out var messageValue) != JavaScriptErrorCode.NoError)
                    throw new Exception("Failed to get error message");

                if (Native.JsStringToPointer(messageValue, out var message, out var length) != JavaScriptErrorCode.NoError)
                    throw new Exception("Failed to convert error message");

                // Throw the readable exception
                var strMessage = Marshal.PtrToStringUni(message);
                throw new Exception(strMessage);
            }

            // Execute promise tasks stored in taskQueue
            while (_taskQueue.Count != 0)
            {
                var task = (JavaScriptValue)_taskQueue.Dequeue();

                Native.JsGetGlobalObject(out JavaScriptValue global);

                JavaScriptValue[] args = new JavaScriptValue[1] { global };
                Native.JsCallFunction(task, args, 1, out JavaScriptValue promiseResult);

                task.Release();
            }

            return result;
        }

        private void SetMethod(JavaScriptValue parent, MethodInfo method, object value)
        {
            // Get the JavaScript name of the method (if the attribute is defined, use that, otherwise
            // use the method name as usual
            var methodName = method.IsDefined(typeof(JavaScriptMethodAttribute))
                ? method.GetCustomAttribute<JavaScriptMethodAttribute>().Name
                : method.Name;

            var methodPropertyId = JavaScriptPropertyId.FromString(methodName);

            // Create the function
            var methodFunction = JavaScriptValue.CreateFunction(new JavaScriptNativeFunction((callee, isConstructCall, arguments, argumentCount, callbackData) =>
            {
                // Get an array of method parameter (used to match up)
                var methodParams = method.GetParameters();

                // Check that the parameter counts are the same
                if ((argumentCount - 1) != methodParams.Length)
                    throw new Exception("Method expected a different number of parameters. TODO, better error message.");

                // Where the method return will be stored.
                object methodReturn;

                // If there is more than 1 argument count, we need to pass in arguments to the
                // method call.
                if (argumentCount > 1)
                {
                    // List to store the parameters for processing
                    var paramList = new List<object>();

                    // Loop through all the arguments (starting with the second item)
                    for (var i = 1; i < arguments.Length; i++)
                    {
                        // If the user passes in null, carry this null onwards
                        if (arguments[i].ValueType == JavaScriptValueType.Null)
                        {
                            paramList.Add(null);
                        }
                        else
                        {
                            // Get the c# parameter type and the JavaScript parameter type
                            var paramType = methodParams[i - 1].ParameterType;
                            var paramObject = FromJavaScriptValue(arguments[i], paramType);
                            paramList.Add(paramObject);
                        }
                    }

                    // Call the method with parameters
                    methodReturn = method.Invoke(value, paramList.ToArray());
                }
                else
                {
                    // Call the method without parameters
                    methodReturn = method.Invoke(value, null);
                }

                // Return invalid as this return type is a void
                if (method.ReturnType == typeof(void))
                    return JavaScriptValue.Invalid;

                // Return the JavaScript value of this return object
                return ToJavaScriptValue(methodReturn);
            }), IntPtr.Zero);

            // Set the property
            parent.SetProperty(methodPropertyId, methodFunction, true);
        }

        #region To JavaScript Value

        public JavaScriptValue ToJavaScriptValue(object value)
        {
            // Get the token
            var token = JToken.FromObject(value);

            if (token == null)
                return JavaScriptValue.Null;

            // Loop through supported tokens
            switch (token.Type)
            {
                case JTokenType.Array:
                    var arrayToken = (JArray)token;
                    var arrayCount = arrayToken.Count;
                    var array = AddRef(JavaScriptValue.CreateArray((uint)arrayCount));

                    for (var i = 0; i < arrayCount; ++i)
                    {
                        var jsValue = ToJavaScriptValue(arrayToken[i]);
                        array.SetIndexedProperty(JavaScriptValue.FromInt32(i), jsValue);
                        jsValue.Release();
                    }

                    return array;

                case JTokenType.Boolean:
                    return token.Value<bool>() ? JavaScriptValue.True : JavaScriptValue.False;

                case JTokenType.Float:
                    return AddRef(JavaScriptValue.FromDouble(token.Value<double>()));

                case JTokenType.Integer:
                    return AddRef(JavaScriptValue.FromDouble(token.Value<double>()));

                case JTokenType.Null:
                    return JavaScriptValue.Null;

                case JTokenType.Object:
                    {
                        var objectToken = (JObject)token;
                        var jsObject = AddRef(JavaScriptValue.CreateObject());

                        // Loop through all properties
                        foreach (var entry in objectToken)
                        {
                            var jsValue = ToJavaScriptValue(entry.Value);
                            var propertyId = JavaScriptPropertyId.FromString(entry.Key);
                            jsObject.SetProperty(propertyId, jsValue, true);
                            jsValue.Release();
                        }

                        // Get the properties and methods
                        var methods = value.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                            .Where(m => !m.IsSpecialName && m.IsPrivate == false).ToList();

                        // Loop through all methods
                        foreach (var method in methods)
                        {
                            SetMethod(jsObject, method, value);
                        }

                        return jsObject;
                    }

                case JTokenType.String:
                    return AddRef(JavaScriptValue.FromString(token.Value<string>()));

                case JTokenType.Undefined:
                    return JavaScriptValue.Undefined;

                default:
                    Console.WriteLine($"Type {token.Type} is not supported!");
                    return JavaScriptValue.Undefined;
            }
        }

        #endregion To JavaScript Value

        private JavaScriptValue AddRef(JavaScriptValue value)
        {
            value.AddRef();
            return value;
        }

        #region From JavaScript Value

        /// <summary>
        ///     Converts a JavaScript value into a c# object (this does not affect private variable
        ///     only public properties / getters and setters).
        /// </summary>
        /// <typeparam name="T">The type of object you are expecting.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted object.</returns>
        public T FromJavaScriptValue<T>(JavaScriptValue value)
        {
            return (T)FromJavaScriptValue(value, typeof(T));
        }

        /// <summary>
        ///     Converts a JavaScript value into a c# object (this does not affect private variable
        ///     only public properties / getters and setters).
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="type">The type of object you are expecting.</param>
        /// <returns>The converted object.</returns>
        public object FromJavaScriptValue(JavaScriptValue value, Type type)
        {
            var jsonObject = JavaScriptValue.GlobalObject.GetProperty(JavaScriptPropertyId.FromString("JSON"));
            var stringify = jsonObject.GetProperty(JavaScriptPropertyId.FromString("stringify"));

            switch (value.ValueType)
            {
                case JavaScriptValueType.Undefined:
                case JavaScriptValueType.Null:
                    return null;

                case JavaScriptValueType.Number:
                    {
                        var number = stringify.CallFunction(JavaScriptValue.GlobalObject, value.ConvertToObject());
                        return JsonConvert.DeserializeObject(number.ToString(), type);
                    }

                case JavaScriptValueType.String:
                    return value.ToString();

                case JavaScriptValueType.Boolean:
                    return value.ToBoolean();

                case JavaScriptValueType.Object:
                    {
                        var number = stringify.CallFunction(JavaScriptValue.GlobalObject, value.ConvertToObject());
                        return JsonConvert.DeserializeObject(number.ToString(), type);
                    }

                case JavaScriptValueType.Function:

                    //    Delegate.CreateDelegate(type, MethodInfo.)

                    //      var func = Expression.Lambda(Expression.Constant(5) Expression.).Compile();

                    ////
                    //     return Activator.CreateInstance(type, func);

                    return new Func<int, int>((t) => { return t + 1; });

                    var method = type.GetMethod("Invoke");

                    return Delegate.CreateDelegate(type, "Hellow", method);

                    // we are passed in a JS function function(x) { return x; }
                    // we don't know the param count, type or the return type, need to
                    // match this to a c# func and return it.

                    var paramTypes = method.GetParameters().Select(param => param.ParameterType).ToArray();

                    if (method.ReturnType == typeof(void))
                    {
                    }
                    else
                    {
                    }

                    Func<dynamic, dynamic> test = new Func<dynamic, dynamic>((i) =>
                    {
                        var funcResult = value.CallFunction();
                        var funcResultU = FromJavaScriptValue(funcResult, typeof(int)); // <--- OUTPUT TYPE
                        return funcResultU;
                    });

                    return null;

                case JavaScriptValueType.Array:
                    {
                        var number = stringify.CallFunction(JavaScriptValue.GlobalObject, value.ConvertToObject());
                        return JsonConvert.DeserializeObject(number.ToString(), type);
                    }

                default:
                    Console.WriteLine($"Type {value.ValueType} is not supported!");
                    return null;
            }
        }

        #endregion From JavaScript Value

        public void Dispose()
        {
            // Dispose runtime
            Native.JsSetCurrentContext(JavaScriptContext.Invalid);
            Native.JsDisposeRuntime(_runtime);
        }
    }
}