using System;
using System.Collections.Generic;
using Celeste.Mod.UI;
using MonoMod.Utils;

namespace Celeste.Mod.ChinaMirror.Utils {
    /// <summary>
    /// Dynamic data wrapper for <see cref="T:Celeste.Mod.UI.AutoModUpdater"/>
    /// </summary>
    public class DDW_AutoModUpdater {

        public DynamicData DynamicData { get; }

        public AutoModUpdater Object => DynamicData.Target as AutoModUpdater;

        public DDW_AutoModUpdater(AutoModUpdater instance) {
            DynamicData = new DynamicData(typeof(AutoModUpdater), instance);
        }

        public string modUpdatingMessage {
            get => DynamicData.Get<string>(nameof(modUpdatingMessage));
            set => DynamicData.Set(nameof(modUpdatingMessage), value);
        }

    }

    /// <summary>
    /// Dynamic data wrapper for <see cref="T:Celeste.Mod.UI.OuiDependencyDownloader"/>
    /// </summary>
    public class DDW_OuiDependencyDownloader {

        public DynamicData DynamicData { get; }

        public object Object => DynamicData.Target;

        public DDW_OuiDependencyDownloader(object instance) {
            DynamicData = new DynamicData(Type.GetType("Celeste.Mod.UI.OuiDependencyDownloader, Celeste"), instance);
        }

        public List<string> Lines {
            get => DynamicData.Get<List<string>>(nameof(Lines));
            set => DynamicData.Set(nameof(Lines), value);
        }

    }
}
