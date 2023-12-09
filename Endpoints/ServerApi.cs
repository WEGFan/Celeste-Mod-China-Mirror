using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Celeste.Mod.ChinaMirror.Endpoints {
    public static class ServerApi {

        private static Encoding UTF8NoBOM = new UTF8Encoding(false);

        public static readonly Uri Host =
#if DEBUG && HOST_LOCALHOST
            new Uri("http://127.0.0.1:8080/");
#elif DEBUG && HOST_DEV
            new Uri("https://celeste-dev.weg.fan/");
#else
            new Uri("https://celeste.weg.fan/");
#endif

        public static string DefaultUserAgent => $"ChinaMirror/{ChinaMirrorModule.Instance.Metadata.VersionString} " +
            $"Celeste/{Celeste.Instance.Version}-{(Everest.Flags.IsFNA ? "fna" : "xna")} " +
            $"Everest/{Everest.VersionString}";

        public static WebClient DefaultClient {
            get {
                WebClient client = new WebClient {
                    Encoding = UTF8NoBOM,
                    Headers = new WebHeaderCollection {
                        [HttpRequestHeader.UserAgent] = DefaultUserAgent
                    },
                    BaseAddress = Host.ToString()
                };
                return client;
            }
        }

        public static Response<FilePrepareStatus> GetMirrorStatus(string type, string fileName) {
            using (WebClient client = DefaultClient) {
                client.QueryString = new NameValueCollection(StringComparer.Ordinal) {
                    ["type"] = type,
                    ["fileName"] = fileName
                };

                byte[] responseData = client.DownloadData("/api/v1/mirror/status");
                Response<FilePrepareStatus> responseObject = JsonConvert.DeserializeObject<Response<FilePrepareStatus>>(UTF8NoBOM.GetString(responseData), new JsonSerializerSettings {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                responseObject.EnsureSucceeded();

                return responseObject;
            }
        }

        public static Response<object> StartDownload(string type, string fileName) {
            using (WebClient client = DefaultClient) {
                client.QueryString = new NameValueCollection(StringComparer.Ordinal) {
                    ["type"] = type,
                    ["fileName"] = fileName
                };

                byte[] responseData = client.UploadData("/api/v1/mirror/start-download", new byte[0]);
                Response<object> responseObject = JsonConvert.DeserializeObject<Response<object>>(UTF8NoBOM.GetString(responseData), new JsonSerializerSettings {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                responseObject.EnsureSucceeded();

                return responseObject;
            }
        }

        public static void EnsureSucceeded<T>(this Response<T> response) {
            if (response.Code != 200) {
                throw new ServerException(response.Code ?? -1, response.Message);
            }
        }

    }
}
