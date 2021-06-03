using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ChinaMirror.Utils {
    public static class LogUtil {
        private const string LoggerTagName = "ChinaMirror";

        public static void Log(string text, LogLevel logLevel = LogLevel.Verbose) {
            Logger.Log(logLevel, LoggerTagName, text);

#if DEBUG
            Color color = logLevel switch {
                LogLevel.Warn => Color.Yellow,
                LogLevel.Error => Color.Red,
                _ => Color.Cyan
            };
            try {
                Engine.Commands?.Log($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{LoggerTagName}] {logLevel}: {text}", color);
            } catch (Exception err) {
                // ignored
            }
#endif
        }
    }
}