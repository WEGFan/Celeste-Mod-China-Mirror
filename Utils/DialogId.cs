namespace Celeste.Mod.ChinaMirror.Utils {
    public static class DialogId {
        private const string Prefix = "ChinaMirror";

        public const string ModName = Prefix + "_mod_name";

        public static class Options {
            private static string Get(string name) => Join(Prefix, "options", name);

            public static readonly string Enabled = Get(nameof(Enabled));
        }

        public static class OptionValues {
            private static string Get(string name) => Join(Prefix, "option_values", name);
        }

        public static class Subtext {
            private static string Get(string name) => Join(Prefix, name, "subtext");
        }

        public static class Text {
            private static string Get(string name) => Join(Prefix, "text", name);

            public static readonly string PreparingFiles = Get(nameof(PreparingFiles));
            public static readonly string WaitTimeout = Get(nameof(WaitTimeout));
        }

        private static string Join(params string[] values) {
            return string.Join("_", values);
        }
    }
}