﻿using System;

namespace ChakraUWP.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class JavaScriptPropertyAttribute : Attribute
    {
        public string Name { get; }

        public JavaScriptPropertyAttribute(string name)
        {
            Name = name;
        }
    }
}