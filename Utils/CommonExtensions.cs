using System;

namespace Celeste.Mod.ChinaMirror.Utils;

internal static class CommonExtensions {

    internal static void Let<T>(this T obj, Action<T> action) {
        action(obj);
    }

    internal static R Let<T, R>(this T obj, Func<T, R> func) {
        return func(obj);
    }

    internal static T Also<T>(this T obj, Action<T> action) {
        action(obj);
        return obj;
    }

}
