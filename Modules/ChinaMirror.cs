using System;
using System.Linq;
using System.Net;
using System.Threading;
using Celeste.Mod.ChinaMirror.Utils;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.ChinaMirror.Modules {
    public static class ChineseMirror {
        public static void Load() {
            IL.Celeste.Mod.Helpers.ModUpdaterHelper.getModUpdaterDatabaseUrl += patch_ModUpdaterHelper_getModUpdaterDatabaseUrl;
            // IL.Celeste.Mod.Everest.Updater.DownloadFileWithProgress += patch_Updater_DownloadFileWithProgress;
            IL.Celeste.Mod.UI.OuiModUpdateList.downloadMod += patch_OuiModUpdateList_downloadMod;
            IL.Celeste.Mod.UI.OuiDependencyDownloader.downloadDependency += patch_OuiDependencyDownloader_downloadDependency;
            IL.Celeste.Mod.UI.AutoModUpdater.autoUpdate += patch_AutoModUpdater_autoUpdate;
        }

        public static void Unload() {
            IL.Celeste.Mod.Helpers.ModUpdaterHelper.getModUpdaterDatabaseUrl -= patch_ModUpdaterHelper_getModUpdaterDatabaseUrl;
            // IL.Celeste.Mod.Everest.Updater.DownloadFileWithProgress -= patch_Updater_DownloadFileWithProgress;
            IL.Celeste.Mod.UI.OuiModUpdateList.downloadMod -= patch_OuiModUpdateList_downloadMod;
            IL.Celeste.Mod.UI.OuiDependencyDownloader.downloadDependency -= patch_OuiDependencyDownloader_downloadDependency;
            IL.Celeste.Mod.UI.AutoModUpdater.autoUpdate -= patch_AutoModUpdater_autoUpdate;
        }

        /// <summary>
        /// Patch <see cref="Helpers.ModUpdaterHelper.getModUpdaterDatabaseUrl"/>.
        /// Change the <c>modupdater.txt</c> url to mirror server.
        /// </summary>
        private static void patch_ModUpdaterHelper_getModUpdaterDatabaseUrl(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdstr("https://everestapi.github.io/modupdater.txt"));
            cursor.Next.Operand = "https://celeste.weg.fan/api/files/modupdater.txt";
        }

        /// <summary>
        /// Patch <see cref="Everest.Updater.DownloadFileWithProgress"/>.
        /// Increase the timeout of <see cref="HttpWebRequest.Timeout"/> and <see cref="HttpWebRequest.ReadWriteTimeout"/>
        /// </summary>
        private static void patch_Updater_DownloadFileWithProgress(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            for (int i = 0; i < 2; i++) {
                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchDup(),
                    instr => instr.MatchLdcI4(10000));
                cursor.Prev.Operand = 3 * 60 * 1000;
            }
        }

        /// <summary>
        /// Patch <see cref="AutoModUpdater.autoUpdate"/>.
        /// Add "server is preparing files" before download starts.
        /// </summary>
        private static void patch_AutoModUpdater_autoUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            /*
                // Logger.Log("AutoModUpdater", "Downloading " + current.URL + " to " + text);
                IL_00b4: ldstr     "AutoModUpdater"
                IL_00b9: ldstr     "Downloading "
                IL_00be: ldloc.s   5
                IL_00c0: callvirt  instance string Celeste.Mod.Helpers.ModUpdateInfo::get_URL()
                IL_00c5: ldstr     " to "
                IL_00ca: ldloc.1
                IL_00cb: call      string [mscorlib]System.String::Concat(string, string, string, string)
                IL_00d0: call      void Celeste.Mod.Logger::Log(string, string)
            */
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchLdstr("AutoModUpdater"),
                instr => instr.MatchLdstr("Downloading "),
                instr => true,
                instr => instr.OpCode == OpCodes.Callvirt &&
                    (instr.Operand as MethodReference).GetID() == "System.String Celeste.Mod.Helpers.ModUpdateInfo::get_URL()",
                instr => instr.MatchLdstr(" to "),
                instr => true,
                instr => instr.MatchCall("System.String", "Concat"),
                instr => instr.OpCode == OpCodes.Call &&
                    (instr.Operand as MethodReference).GetID() == "System.Void Celeste.Mod.Logger::Log(System.String,System.String)");

            VariableReference var_ModUpdateInfo = il.Body.Variables
                .First(var => var.VariableType.FullName == "Celeste.Mod.Helpers.ModUpdateInfo");
            VariableReference var_progressString_DisplayClass = null;
            FieldReference f_progressString = null;
            foreach (VariableDefinition variable in il.Body.Variables) {
                FieldReference field = variable.VariableType.SafeResolve()
                    .FindField("progressString");
                if (field?.FieldType.FullName == "System.String") {
                    var_progressString_DisplayClass = variable;
                    f_progressString = field;
                    break;
                }
            }

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, var_ModUpdateInfo);
            cursor.Emit(OpCodes.Ldloc, var_progressString_DisplayClass);
            cursor.Emit(OpCodes.Ldfld, f_progressString);
            cursor.EmitDelegate<Action<AutoModUpdater, ModUpdateInfo, string>>((self, modUpdateInfo, progressString) => {
                DDW_AutoModUpdater selfWrapper = new DDW_AutoModUpdater(self);

                string filename = modUpdateInfo.URL.Split('/').Last();
                using (WebClient client = new WebClient()) {
                    LogUtil.Log($"{filename} - started downloading on server", LogLevel.Info);
                    client.DownloadString($"https://celeste.weg.fan/api/start/{filename}");
                    DateTime startTime = DateTime.Now;
                    LogUtil.Log($"{filename} - checking server status", LogLevel.Info);
                    while (true) {
                        string status = client.DownloadString($"https://celeste.weg.fan/api/status/{filename}").Trim();
                        bool modReady = status == "1";
                        if (modReady) {
                            break;
                        }
                        LogUtil.Log($"{filename} - waiting for server preparing files ({(DateTime.Now - startTime).TotalSeconds:F2}s)", LogLevel.Info);
                        if (DateTime.Now - startTime >= TimeSpan.FromMinutes(1)) {
                            LogUtil.Log($"{filename} - waiting for server preparing files timeout", LogLevel.Warn);
                            selfWrapper.modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_DOWNLOADING")} ({Dialog.Clean(DialogId.Text.WaitTimeout)})";
                            throw new TimeoutException("Waiting for server preparing files timeout");
                        }
                        selfWrapper.modUpdatingMessage = $"{progressString} {Dialog.Clean("AUTOUPDATECHECKER_DOWNLOADING")} ({Dialog.Clean(DialogId.Text.PreparingFiles)})";
                        Thread.Sleep(2000);
                    }
                    LogUtil.Log($"{filename} - start downloading", LogLevel.Info);
                }
            });
        }

        /// <summary>
        /// Patch <see cref="OuiDependencyDownloader.downloadDependency"/>.
        /// Add "server is preparing files" before download starts.
        /// </summary>
        private static void patch_OuiDependencyDownloader_downloadDependency(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            /*
                // LogLine("", logToLogger: false);
                IL_0033: ldarg.0
                IL_0034: ldstr     ""
                IL_0039: ldc.i4.0
                IL_003a: call      instance void Celeste.Mod.UI.OuiLoggedProgress::LogLine(string, bool) 
            */
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdstr(""),
                instr => instr.MatchLdcI4(0),
                instr => instr.OpCode == OpCodes.Call &&
                    (instr.Operand as MethodReference).GetID() == "System.Void Celeste.Mod.UI.OuiLoggedProgress::LogLine(System.String,System.Boolean)");

            ParameterReference p_ModUpdateInfo = il.Method.Parameters
                .First(param => param.ParameterType.FullName == "Celeste.Mod.Helpers.ModUpdateInfo");

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg, p_ModUpdateInfo);
            cursor.EmitDelegate<Action<object, ModUpdateInfo>>((self, modUpdateInfo) => {
                DDW_OuiDependencyDownloader selfWrapper = new DDW_OuiDependencyDownloader(self);

                string filename = modUpdateInfo.URL.Split('/').Last();
                using (WebClient client = new WebClient()) {
                    LogUtil.Log($"{filename} - started downloading on server", LogLevel.Info);
                    client.DownloadString($"https://celeste.weg.fan/api/start/{filename}");
                    DateTime startTime = DateTime.Now;
                    LogUtil.Log($"{filename} - checking server status", LogLevel.Info);
                    while (true) {
                        string status = client.DownloadString($"https://celeste.weg.fan/api/status/{filename}").Trim();
                        bool modReady = status == "1";
                        if (modReady) {
                            break;
                        }
                        LogUtil.Log($"{filename} - waiting for server preparing files ({(DateTime.Now - startTime).TotalSeconds:F2}s)", LogLevel.Info);

                        if (DateTime.Now - startTime >= TimeSpan.FromMinutes(1)) {
                            LogUtil.Log($"{filename} - waiting for server preparing files timeout", LogLevel.Warn);
                            selfWrapper.Lines[selfWrapper.Lines.Count - 1] = Dialog.Clean(DialogId.Text.WaitTimeout);
                            throw new TimeoutException("Waiting for server preparing files timeout");
                        }
                        selfWrapper.Lines[selfWrapper.Lines.Count - 1] = Dialog.Clean(DialogId.Text.PreparingFiles);
                        Thread.Sleep(2000);
                    }
                    LogUtil.Log($"{filename} - start downloading", LogLevel.Info);
                }
            });
        }

        /// <summary>
        /// Patch <see cref="OuiModUpdateList.downloadMod"/>.
        /// Add "server is preparing files" before download starts.
        /// </summary>
        private static void patch_OuiModUpdateList_downloadMod(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            /*
                // Logger.Log("OuiModUpdateList", "Downloading " + <>c__DisplayClass20_.update.URL + " to " + zipPath);
                IL_0014: ldstr     "OuiModUpdateList"
                IL_0019: ldstr     "Downloading "
                IL_001e: ldloc.0
                IL_001f: ldfld     class Celeste.Mod.Helpers.ModUpdateInfo Celeste.Mod.UI.OuiModUpdateList/'<>c__DisplayClass20_0'::update
                IL_0024: callvirt  instance string Celeste.Mod.Helpers.ModUpdateInfo::get_URL()
                IL_0029: ldstr     " to "
                IL_002e: ldarg.2
                IL_002f: call      string [mscorlib]System.String::Concat(string, string, string, string)
                IL_0034: call      void Celeste.Mod.Logger::Log(string, string)
            */
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchLdstr("OuiModUpdateList"),
                instr => instr.MatchLdstr("Downloading "),
                instr => true,
                instr => true,
                instr => instr.OpCode == OpCodes.Callvirt &&
                    (instr.Operand as MethodReference).GetID() == "System.String Celeste.Mod.Helpers.ModUpdateInfo::get_URL()",
                instr => instr.MatchLdstr(" to "),
                instr => true,
                instr => instr.MatchCall("System.String", "Concat"),
                instr => instr.OpCode == OpCodes.Call &&
                    (instr.Operand as MethodReference).GetID() == "System.Void Celeste.Mod.Logger::Log(System.String,System.String)");

            ParameterReference p_ModUpdateInfo = il.Method.Parameters
                .First(param => param.ParameterType.FullName == "Celeste.Mod.Helpers.ModUpdateInfo");
            ParameterReference p_TextMenu_Button = il.Method.Parameters
                .First(param => param.ParameterType.FullName == "Celeste.TextMenu/Button");

            cursor.Emit(OpCodes.Ldarg, p_ModUpdateInfo);
            cursor.Emit(OpCodes.Ldarg, p_TextMenu_Button);
            cursor.EmitDelegate<Action<ModUpdateInfo, TextMenu.Button>>((modUpdateInfo, button) => {
                string filename = modUpdateInfo.URL.Split('/').Last();
                using (WebClient client = new WebClient()) {
                    LogUtil.Log($"{filename} - started downloading on server", LogLevel.Info);
                    client.DownloadString($"https://celeste.weg.fan/api/start/{filename}");
                    DateTime startTime = DateTime.Now;
                    LogUtil.Log($"{filename} - checking server status", LogLevel.Info);
                    while (true) {
                        string status = client.DownloadString($"https://celeste.weg.fan/api/status/{filename}").Trim();
                        bool modReady = status == "1";
                        if (modReady) {
                            break;
                        }
                        LogUtil.Log($"{filename} - waiting for server preparing files ({(DateTime.Now - startTime).TotalSeconds:F2}s)", LogLevel.Info);
                        if (DateTime.Now - startTime >= TimeSpan.FromMinutes(1)) {
                            LogUtil.Log($"{filename} - waiting for server preparing files timeout", LogLevel.Warn);
                            button.Label = $"{ModUpdaterHelper.FormatModName(modUpdateInfo.Name)} ({Dialog.Clean(DialogId.Text.WaitTimeout)})";
                            throw new TimeoutException("Waiting for server preparing files timeout");
                        }
                        button.Label = $"{ModUpdaterHelper.FormatModName(modUpdateInfo.Name)} ({Dialog.Clean(DialogId.Text.PreparingFiles)})";
                        Thread.Sleep(2000);
                    }
                    LogUtil.Log($"{filename} - start downloading", LogLevel.Info);
                }
            });
        }
    }
}