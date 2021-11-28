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

        public static bool Loaded = false;

        public override void Load() {
            if (Loaded || !Settings.Enabled) {
                return;
            }
            Modules.ChinaMirror.Load();
            Loaded = true;
        }

        public override void Unload() {
            if (!Loaded) {
                return;
            }
            Modules.ChinaMirror.Unload();
            Loaded = false;
        }

    }
}
