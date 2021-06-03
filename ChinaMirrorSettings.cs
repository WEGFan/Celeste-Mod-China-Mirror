using Celeste.Mod.ChinaMirror.Utils;

namespace Celeste.Mod.ChinaMirror {
    [SettingName(DialogId.ModName)]
    public class ChinaMirrorSettings : EverestModuleSettings {
        public bool Enabled { get; set; } = true;

        public void CreateEnabledEntry(TextMenu textMenu, bool inGame) {
            TextMenu.Item item = new TextMenu.OnOff(Dialog.Clean(DialogId.Options.Enabled), Enabled)
                .Change(value => {
                    Enabled = value;
                    if (value) {
                        ChinaMirrorModule.Instance.Load();
                    } else {
                        ChinaMirrorModule.Instance.Unload();
                    }
                });
            textMenu.Add(item);
        }
    }
}