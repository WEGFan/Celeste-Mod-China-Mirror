using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Celeste.Mod.ChinaMirror.Endpoints;
using Celeste.Mod.ChinaMirror.Utils;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.ChinaMirror.Modules {
    public static class ChinaMirror {

        private static readonly List<IDetour> hooks = new List<IDetour>();

        public static void Load() {
            // undo hooks when exception occurs, so we need to manually apply hooks
            ILHookConfig ilHookConfig = new ILHookConfig {ManualApply = true};
            try {
                hooks.Add(new ILHook(typeof(ModUpdaterHelper).FindMethod("getModUpdaterDatabaseUrl"),
                    IL_ModUpdaterHelper_getModUpdaterDatabaseUrl, ilHookConfig));
                hooks.Add(new ILHook(typeof(ModUpdaterHelper).FindMethod("DownloadModUpdateList"),
                    IL_ModUpdaterHelper_DownloadModUpdateList, ilHookConfig));
                hooks.Add(new ILHook(Type.GetType("Celeste.Mod.UI.OuiModUpdateList, Celeste").FindMethod("downloadMod"),
                    IL_OuiModUpdateList_downloadMod, ilHookConfig));
                hooks.Add(new ILHook(Type.GetType("Celeste.Mod.UI.OuiDependencyDownloader, Celeste").FindMethod("downloadDependency"),
                    IL_OuiDependencyDownloader_downloadDependency, ilHookConfig));
                hooks.Add(new ILHook(Type.GetType("Celeste.Mod.UI.AutoModUpdater, Celeste").FindMethod("autoUpdate"),
                    IL_AutoModUpdater_autoUpdate, ilHookConfig));
                hooks.Add(new ILHook(Type.GetType("Celeste.Mod.Everest+Updater, Celeste").FindMethod("DownloadFileWithProgress"),
                    IL_Updater_DownloadFileWithProgress, ilHookConfig));

                hooks.ForEach(hook => hook.Apply());
            } catch (Exception e) {
                LogUtil.Log("failed to hook", LogLevel.Error);
                Logger.LogDetailed(e);

                Unload();
            }
        }

        public static void Unload() {
            hooks.ForEach(hook => hook?.Dispose());
            hooks.Clear();
        }

        private static void IL_ModUpdaterHelper_getModUpdaterDatabaseUrl(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // change the modupdater.txt url to mirror server
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdstr(out string str) && str.Contains("https://everestapi.github.io/"));

            string newUrl = ((string)cursor.Next.Operand)
                .Let(it => it.Replace("https://everestapi.github.io/", new Uri(ServerApi.Host, "/api/v1/file/").ToString()));
            cursor.Next.Operand = newUrl;
        }

        private static void IL_ModUpdaterHelper_DownloadModUpdateList(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // deserialize yaml to ModUpdateInfoExtended instead to add additional fields
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

        private static void IL_AutoModUpdater_autoUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // add "server is preparing files" before download starts
            /*
                // Everest.Updater.DownloadFileWithProgress(current.URL, text, progressCallback);
                IL_00e4: ldloc.s   5
                IL_00e6: callvirt  instance string Celeste.Mod.Helpers.ModUpdateInfo::get_URL()
                IL_00eb: ldloc.1
                IL_00ec: ldloc.s   7
                IL_00ee: call      void Celeste.Mod.Everest/Updater::DownloadFileWithProgress(string, string, class [mscorlib]System.Func`4<int32, int64, int32, bool>)
            */
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchCallvirt("Celeste.Mod.Helpers.ModUpdateInfo", "get_URL"),
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchCall("Celeste.Mod.Everest/Updater", "DownloadFileWithProgress"));

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

            Instruction oldTryStart = cursor.Next;
            cursor.Emit(OpCodes.Ldarg_0);
            Instruction newTryStart = cursor.Prev;
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

            // MoveAfterLabels doesn't work for exception handlers, manually move them instead
            foreach (ExceptionHandler handler in il.Body.ExceptionHandlers) {
                if (handler.TryStart == oldTryStart) {
                    handler.TryStart = newTryStart;
                }
            }
        }

        private static void IL_OuiDependencyDownloader_downloadDependency(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // add "server is preparing files" before download starts
            /*
                // Everest.Updater.DownloadFileWithProgress(mod.URL, text, progressCallback);
                IL_004c: ldarg.1
                IL_004d: callvirt  instance string Celeste.Mod.Helpers.ModUpdateInfo::get_URL()
                IL_0052: ldloc.0
                IL_0053: ldloc.1
                IL_0054: call      void Celeste.Mod.Everest/Updater::DownloadFileWithProgress(string, string, class [mscorlib]System.Func`4<int32, int64, int32, bool>)
            */
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdarg(out int _),
                instr => instr.MatchCallvirt("Celeste.Mod.Helpers.ModUpdateInfo", "get_URL"),
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchCall("Celeste.Mod.Everest/Updater", "DownloadFileWithProgress"));

            ParameterReference p_ModUpdateInfo = il.Method.Parameters
                .First(param => param.ParameterType.FullName == "Celeste.Mod.Helpers.ModUpdateInfo");

            Instruction oldTryStart = cursor.Next;
            cursor.Emit(OpCodes.Ldarg_0);
            Instruction newTryStart = cursor.Prev;
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

            // MoveAfterLabels doesn't work for exception handlers, manually move them instead
            foreach (ExceptionHandler handler in il.Body.ExceptionHandlers) {
                if (handler.TryStart == oldTryStart) {
                    handler.TryStart = newTryStart;
                }
            }
        }

        private static void IL_OuiModUpdateList_downloadMod(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // add "server is preparing files" before download starts
            /*
                // Everest.Updater.DownloadFileWithProgress(update.URL, zipPath, progressCallback);
                IL_0046: ldloc.0
                IL_0047: ldfld     class Celeste.Mod.Helpers.ModUpdateInfo Celeste.Mod.UI.OuiModUpdateList/'<>c__DisplayClass20_0'::update
                IL_004c: callvirt  instance string Celeste.Mod.Helpers.ModUpdateInfo::get_URL()
                IL_0051: ldarg.2
                IL_0052: ldloc.1
                IL_0053: call      void Celeste.Mod.Everest/Updater::DownloadFileWithProgress(string, string, class [mscorlib]System.Func`4<int32, int64, int32, bool>)
            */
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchLdfld(out FieldReference f) && f is {FieldType: {FullName: "Celeste.Mod.Helpers.ModUpdateInfo"}, Name: "update"},
                instr => instr.MatchCallvirt("Celeste.Mod.Helpers.ModUpdateInfo", "get_URL"),
                instr => instr.MatchLdarg(out int _),
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchCall("Celeste.Mod.Everest/Updater", "DownloadFileWithProgress"));

            ParameterReference p_ModUpdateInfo = il.Method.Parameters
                .First(param => param.ParameterType.FullName == "Celeste.Mod.Helpers.ModUpdateInfo");
            ParameterReference p_TextMenu_Button = il.Method.Parameters
                .First(param => param.ParameterType.FullName == "Celeste.TextMenu/Button");

            Instruction oldTryStart = cursor.Next;
            cursor.Emit(OpCodes.Ldarg, p_ModUpdateInfo);
            Instruction newTryStart = cursor.Prev;
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

            // MoveAfterLabels doesn't work for exception handlers, manually move them instead
            foreach (ExceptionHandler handler in il.Body.ExceptionHandlers) {
                if (handler.TryStart == oldTryStart) {
                    handler.TryStart = newTryStart;
                }
            }
        }

        private static void IL_Updater_DownloadFileWithProgress(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // .NET Core disallows follow redirection from HTTPS to HTTP automatically, so we need to manually get the redirected URL
            if (Environment.Version.Major != 4) {
                cursor.Emit(OpCodes.Ldarga, 0);
                cursor.EmitDelegate((ref string url) => {
                    for (int i = 0; i < 10; i++) {
                        HttpWebRequest request = WebRequest.CreateHttp(url);
                        request.Method = "HEAD";
                        HttpWebResponse response;
                        try {
                            response = (HttpWebResponse)request.GetResponse();
                        } catch (WebException e) {
                            response = (HttpWebResponse)e.Response;
                        }

                        if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or
                            HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect
                        ) {
                            url = response.Headers[HttpResponseHeader.Location] ?? "";
                        } else {
                            break;
                        }
                    }
                });
            }
        }

        public delegate void PrepareFileProgressCallback(long current, long total, bool timeout);

        private static void PrepareFile(ModUpdateInfo modUpdateInfo, PrepareFileProgressCallback progressCallback) {
            if (modUpdateInfo is not ModUpdateInfoExtended info) {
                LogUtil.Log($"{modUpdateInfo.Name} - mod update info is not ModUpdateInfoExtended", LogLevel.Warn);
                return;
            }
            string fileName = info.MirrorFileName;
            LogUtil.Log($"{fileName} - started downloading on server", LogLevel.Info);
            ServerApi.StartDownload(info.MirrorType, fileName);

            DateTime startTime = DateTime.Now;
            DateTime previousCurrentTime = DateTime.Now;
            long previousCurrent = 0;
            while (true) {
                Response<FilePrepareStatus> statusResponse = ServerApi.GetMirrorStatus(info.MirrorType, fileName);
                FilePrepareStatus progress = statusResponse.Data;
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
                Thread.Sleep(200);
            }
            LogUtil.Log($"{fileName} - start downloading", LogLevel.Info);
        }

    }
}
