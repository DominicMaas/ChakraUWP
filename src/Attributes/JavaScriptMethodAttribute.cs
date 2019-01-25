using System;

namespace SoundByte.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class JavaScriptMethodAttribute : Attribute
    {
        public string Name { get; }

        public JavaScriptMethodAttribute(string name)
        {
            Name = name;
        }
    }
}