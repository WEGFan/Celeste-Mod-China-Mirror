using System;
using System.Collections.Generic;
using Celeste.Mod.UI;
using MonoMod.Utils;

namespace Celeste.Mod.ChinaMirror.Utils {
    public abstract class BaseDynamicDataWrapper {
        protected BaseDynamicDataWrapper(Type type) {
            DynamicData = new DynamicData(type);
        }

        protected BaseDynamicDataWrapper(object obj) {
            DynamicData = new DynamicData(obj);
        }

        protected BaseDynamicDataWrapper(Type type, object obj) {
            DynamicData = new DynamicData(type, obj);
        }

        public DynamicData DynamicData { get; }

        public object Object => DynamicData.Target;
    }

    public class AutoModUpdaterWrapper : BaseDynamicDataWrapper {
        public AutoModUpdaterWrapper(AutoModUpdater instance) : base(instance) {
        }

        public string modUpdatingMessage {
            get => DynamicData.Get<string>(nameof(modUpdatingMessage));
            set => DynamicData.Set(nameof(modUpdatingMessage), value);
        }
    }

    public class OuiDependencyDownloaderWrapper : BaseDynamicDataWrapper {
        public OuiDependencyDownloaderWrapper(object instance) : base(instance) {
        }

        public List<string> Lines {
            get => DynamicData.Get<List<string>>(nameof(Lines));
            set => DynamicData.Set(nameof(Lines), value);
        }
    }
}