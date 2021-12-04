using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace JLChnToZ.VRC.UdonKatana {
    internal static class Helper {
        private delegate string ToolbarSearchFieldDelegate(string text, params GUILayoutOption[] options);
        private static readonly ToolbarSearchFieldDelegate toolbarSearchField = GetDelegate<EditorGUILayout, ToolbarSearchFieldDelegate>("ToolbarSearchField");

        public static string ToolbarSearchField(string text, params GUILayoutOption[] options) => toolbarSearchField(text, options);

        public static TDelegate GetDelegate<TDelegate>(string fromTypeName, string methodName, object target = null) where TDelegate : Delegate {
            if (fromTypeName == null) throw new ArgumentNullException(nameof(fromTypeName));
            Type fromType = Type.GetType(fromTypeName, false);
            if (fromType == null) return null;
            return GetDelegate<TDelegate>(fromType, methodName, target);
        }

        public static TDelegate GetDelegate<TDelegate>(Type fromType, string methodName, object target = null) where TDelegate : Delegate {
            if (fromType == null)
                throw new ArgumentNullException(nameof(fromType));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));
            Type delegateType = typeof(TDelegate);
            MethodInfo method = FindMethod(fromType, methodName, delegateType);
            if (method == null)
                return null;
            if (method.IsStatic)
                return Delegate.CreateDelegate(delegateType, method, false) as TDelegate;
            if (target == null && fromType.IsValueType)
                target = Activator.CreateInstance(fromType);
            return Delegate.CreateDelegate(delegateType, target, method, false) as TDelegate;
        }

        public static TDelegate GetDelegate<TFrom, TDelegate>(string methodName, TFrom target = default) where TDelegate : Delegate {
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));
            Type delegateType = typeof(TDelegate);
            MethodInfo method = FindMethod(typeof(TFrom), methodName, delegateType);
            if (method == null)
                return null;
            if (method.IsStatic)
                return Delegate.CreateDelegate(delegateType, method, false) as TDelegate;
            return Delegate.CreateDelegate(delegateType, target, method, false) as TDelegate;
        }

        private static MethodInfo FindMethod(Type fromType, string methodName, Type delegateType) {
            const string NotADelegateMsg = "{0} is not a delegate.";
            const string MissingInvokeMsg =
                "Cannot determine what parameters does {0} have, " +
                "as no Invoke(...) signature found. " +
                "Perhaps this is not a valid delegate.";
            if (!delegateType.IsSubclassOf(typeof(Delegate)))
                throw new ArgumentException(string.Format(NotADelegateMsg, delegateType.Name));
            MethodInfo invokeMethod = delegateType.GetMethod("Invoke");
            if (invokeMethod == null)
                throw new ArgumentException(string.Format(MissingInvokeMsg, delegateType.Name));
            return fromType.GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                null, CallingConventions.Any,
                Array.ConvertAll(invokeMethod.GetParameters(), p => p.ParameterType),
                null
            );
        }
        
        public static string ToCSharpTypeName(this Type type) {
            if (type.Equals(typeof(void))) return "void";
            if (type.Equals(typeof(char))) return "char";
            if (type.Equals(typeof(bool))) return "bool";
            if (type.Equals(typeof(byte))) return "byte";
            if (type.Equals(typeof(sbyte))) return "sbyte";
            if (type.Equals(typeof(short))) return "short";
            if (type.Equals(typeof(ushort))) return "ushort";
            if (type.Equals(typeof(int))) return "int";
            if (type.Equals(typeof(uint))) return "uint";
            if (type.Equals(typeof(long))) return "long";
            if (type.Equals(typeof(ulong))) return "ulong";
            if (type.Equals(typeof(float))) return "float";
            if (type.Equals(typeof(double))) return "double";
            if (type.Equals(typeof(decimal))) return "decimal";
            if (type.Equals(typeof(object))) return "object";
            if (type.Equals(typeof(string))) return "string";
            if (type.IsArray) return $"{type.GetElementType().ToCSharpTypeName()}[]";
            if (type.IsGenericType) return $"{type.Name.Substring(0, type.Name.LastIndexOf("`", StringComparison.Ordinal))}<{string.Join(", ", type.GetGenericArguments().Select(ToCSharpTypeName))}>";
            return type.Name;
        }

        public static int CalcluateLines(string src) {
            int count = 0, offset = -1;
            do {
                count++;
                offset = src.IndexOf('\n', offset + 1);
            } while (offset >= 0);
            return count;
        }
    }
}