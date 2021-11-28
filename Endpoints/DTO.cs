using Celeste.Mod.Helpers;

namespace Celeste.Mod.ChinaMirror.Endpoints {
    public record Response<T> {

        public T Data { get; set; }

        public int? Code { get; set; }

        public string Message { get; set; }

    }

    public record MirrorFileType {

        public static readonly MirrorFileType Mod = new MirrorFileType("mod");
        public static readonly MirrorFileType Everest = new MirrorFileType("everest");

        public string Tag { get; }

        private MirrorFileType(string tag) {
            Tag = tag;
        }

    }

    public record FilePrepareStatus {

        public FileProgress DownloadProgress { get; set; }

        public FileProgress UploadProgress { get; set; }

    }

    public record FileProgress {

        public long Current { get; set; }

        public long Total { get; set; }

    }

    public class ModUpdateInfoExtended : ModUpdateInfo {

        public virtual string MirrorType { get; set; }

        public virtual string MirrorFileName { get; set; }

        public override string ToString() {
            return $"{nameof(ModUpdateInfoExtended)} {{ " +
                $"{nameof(Name)} = {Name}, " +
                $"{nameof(Version)} = {Version}, " +
                $"{nameof(LastUpdate)} = {LastUpdate}, " +
                $"{nameof(URL)} = {URL}, " +
                $"{nameof(MirrorURL)} = {MirrorURL}, " +
                $"{nameof(xxHash)} = {xxHash}, " +
                $"{nameof(GameBananaType)} = {GameBananaType}, " +
                $"{nameof(GameBananaId)} = {GameBananaId}, " +
                $"{nameof(MirrorType)} = {MirrorType}, " +
                $"{nameof(MirrorFileName)} = {MirrorFileName} " +
                "}";
        }

    }
}
