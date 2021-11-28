using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Celeste.Mod.ChinaMirror.Endpoints;
using Celeste.Mod.ChinaMirror.Utils;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RestSharp;

namespace Celeste.Mod.ChinaMirror.Modules {
    public static class ChineseMirror {
        public static void Load() {
            IL.Celeste.Mod.Helpers.ModUpdaterHelper.getModUpdaterDatabaseUrl += patch_ModUpdaterHelper_getModUpdaterDatabaseUrl;
            IL.Celeste.Mod.Helpers.ModUpdaterHelper.DownloadModUpdateList += patch_ModUpdaterHelper_DownloadModUpdateList;
            IL.Celeste.Mod.UI.OuiModUpdateList.downloadMod += patch_OuiModUpdateList_downloadMod;
            IL.Celeste.Mod.UI.OuiDependencyDownloader.downloadDependency += patch_OuiDependencyDownloader_downloadDependency;
            IL.Celeste.Mod.UI.AutoModUpdater.autoUpdate += patch_AutoModUpdater_autoUpdate;
        }

        public static void Unload() {
            IL.Celeste.Mod.Helpers.ModUpdaterHelper.getModUpdaterDatabaseUrl -= patch_ModUpdaterHelper_getModUpdaterDatabaseUrl;
            IL.Celeste.Mod.Helpers.ModUpdaterHelper.DownloadModUpdateList -= patch_ModUpdaterHelper_DownloadModUpdateList;
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
            cursor.Next.Operand = new Uri(ServerApi.Host, "/api/v1/file/modupdater.txt").ToString();
        }

        /// <summary>
        /// Patch <see cref="Helpers.ModUpdaterHelper.DownloadModUpdateList"/>.
        /// Deserialize yaml to <see cref="ModUpdateInfoExtended"/> instead to add additional fields.
        /// </summary>
        private static void patch_ModUpdaterHelper_DownloadModUpdateList(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            /*
                // string input = webClient.DownloadString(modUpdaterDatabaseUrl);
                IL_0023: ldloc.2
                IL_0024: ldloc.1
                IL_0025: callvirt  instance string [System]System.Net.WebClient::DownloadString(string)
                IL_002a: stloc.3
                // dictionary = YamlHelper.Deserializer.Deserialize<Dictionary<string, ModUpdateInfo>>(input);
                IL_002b: ldsfld    class [YamlDotNet]YamlDotNet.Serialization.IDeserializer Celeste.Mod.YamlHelper::Deserializer
                IL_0030: ldloc.3
                IL_0031: callvirt  instance !!0 [YamlDotNet]YamlDotNet.Serialization.IDeserializer::Deserialize<class [mscorlib]System.Collections.Generic.Dictionary`2<string, class Celeste.Mod.Helpers.ModUpdateInfo>>(string)
                IL_0036: stloc.0
            */
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdsfld("Celeste.Mod.YamlHelper", "Deserializer"));
            cursor.Prev.MatchStloc(out int var_yamlData_index);
            VariableReference var_yamlData = il.Body.Variables[var_yamlData_index];

            cursor.GotoNext(MoveType.After,
                instr => instr.OpCode == OpCodes.Callvirt &&
                    (instr.Operand as MethodReference).GetID() == "T YamlDotNet.Serialization.IDeserializer::Deserialize<System.Collections.Generic.Dictionary`2<System.String,Celeste.Mod.Helpers.ModUpdateInfo>>(System.String)");

            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldloc, var_yamlData);
            cursor.EmitDelegate<Func<string, Dictionary<string, ModUpdateInfo>>>(yamlData => {
                return YamlHelper.Deserializer.Deserialize<Dictionary<string, ModUpdateInfoExtended>>(yamlData)
                    .ToDictionary(kvp => kvp.Key, kvp => (ModUpdateInfo)kvp.Value);
            });
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

                PrepareFile(modUpdateInfo, (current, total, timeout) => {
                    if (timeout) {
                        selfWrapper.modUpdatingMessage = $"{progressString} {Dialog.Clean(DialogId.Text.WaitTimeout)}";
                        return;
                    }
                    selfWrapper.modUpdatingMessage = $"{progressString} {Dialog.Clean(DialogId.Text.PreparingFiles)}";
                    if (total != 0) {
                        float percent = 100f * current / total;
                        selfWrapper.modUpdatingMessage += $" ({percent:F0}%)";
                    }
                });
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

                PrepareFile(modUpdateInfo, (current, total, timeout) => {
                    if (timeout) {
                        selfWrapper.Lines[selfWrapper.Lines.Count - 1] = Dialog.Clean(DialogId.Text.WaitTimeout);
                        return;
                    }
                    selfWrapper.Lines[selfWrapper.Lines.Count - 1] = Dialog.Clean(DialogId.Text.PreparingFiles);
                    if (total != 0) {
                        float percent = 100f * current / total;
                        selfWrapper.Lines[selfWrapper.Lines.Count - 1] += $" ({percent:F0}%)";
                    }
                });
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
                PrepareFile(modUpdateInfo, (current, total, timeout) => {
                    if (timeout) {
                        button.Label = $"{ModUpdaterHelper.FormatModName(modUpdateInfo.Name)} ({Dialog.Clean(DialogId.Text.WaitTimeout)})";
                        return;
                    }
                    button.Label = $"{ModUpdaterHelper.FormatModName(modUpdateInfo.Name)} ({Dialog.Clean(DialogId.Text.PreparingFiles)})";
                    if (total != 0) {
                        float percent = 100f * current / total;
                        button.Label = $"{ModUpdaterHelper.FormatModName(modUpdateInfo.Name)} ({Dialog.Clean(DialogId.Text.PreparingFiles)} {percent:F0}%)";
                    }
                });
            });
        }

        public delegate void PrepareFileProgressCallback(long current, long total, bool timeout);

        private static void PrepareFile(ModUpdateInfo modUpdateInfo, PrepareFileProgressCallback progressCallback) {
            if (modUpdateInfo is not ModUpdateInfoExtended info) {
                LogUtil.Log($"{modUpdateInfo.Name} - mod update info is not ModUpdateInfoExtended", LogLevel.Warn);
                return;
            }
            string fileName = info.MirrorFileName;
            LogUtil.Log($"{fileName} - started downloading on server", LogLevel.Info);
            ServerApi.StartDownload(MirrorFileType.Mod, fileName);

            DateTime startTime = DateTime.Now;
            DateTime previousCurrentTime = DateTime.Now;
            long previousCurrent = 0;
            while (true) {
                IRestResponse<Response<FilePrepareStatus>> statusResponse = ServerApi.GetMirrorStatus(MirrorFileType.Mod, fileName);
                FilePrepareStatus progress = statusResponse.Data.Data;
                if (progress.UploadProgress.Current > 0) {
                    // assume download is completed if the upload is started
                    progress.DownloadProgress.Current = progress.DownloadProgress.Total;
                }
                long current = progress.DownloadProgress.Current + progress.UploadProgress.Current;
                long total = progress.DownloadProgress.Total + progress.UploadProgress.Total;
                progressCallback(current, total, false);
                if (total != 0 && current == total) {
                    break;
                }
                if (current != previousCurrent) {
                    previousCurrent = current;
                    previousCurrentTime = DateTime.Now;
                }

                LogUtil.Log($"{fileName} - waiting for server preparing files {(DateTime.Now - startTime).TotalSeconds:F2}s ({progress})", LogLevel.Info);

                bool timeout = total == 0
                    ? DateTime.Now - startTime >= TimeSpan.FromSeconds(30)
                    : DateTime.Now - previousCurrentTime >= TimeSpan.FromSeconds(10);
                if (timeout) {
                    LogUtil.Log($"{fileName} - waiting for server preparing files timeout", LogLevel.Warn);
                    progressCallback(current, total, true);
                    throw new TimeoutException("Waiting for server preparing files timeout");
                }
                Thread.Sleep(2000);
            }
            LogUtil.Log($"{fileName} - start downloading", LogLevel.Info);
        }
    }
}
