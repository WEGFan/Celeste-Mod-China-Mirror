using System;
using Celeste.Mod.ChinaMirror.Modules;

namespace Celeste.Mod.ChinaMirror {
    public class ChinaMirrorModule : EverestModule {
        public ChinaMirrorModule() {
            Instance = this;
        }

        public static ChinaMirrorModule Instance { get; private set; }

        public override Type SettingsType => typeof(ChinaMirrorSettings);

        public static ChinaMirrorSettings Settings => Instance._Settings as ChinaMirrorSettings;

        public static bool Hooked = false;

        public override void Load() {
            if (Hooked || !Settings.Enabled) {
                return;
            }
            ChineseMirror.Load();
            Hooked = true;
        }

        public override void Unload() {
            if (!Hooked) {
                return;
            }
            ChineseMirror.Unload();
            Hooked = false;
        }
    }
}