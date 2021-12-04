using System;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using RestSharp.Extensions;
using RestSharp.Serializers.NewtonsoftJson;

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

        public static RestClient DefaultClient {
            get {
                RestClient client = new RestClient {
                    UserAgent = DefaultUserAgent,
                    Encoding = UTF8NoBOM
                };
                client.UseNewtonsoftJson(new JsonSerializerSettings {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                return client;
            }
        }

        public static IRestResponse<Response<FilePrepareStatus>> GetMirrorStatus(string type, string fileName) {
            Uri url = new Uri(Host, "/api/v1/mirror/status");
            IRestRequest request = new RestRequest(url, Method.GET)
                .AddQueryParameter("type", type)
                .AddQueryParameter("fileName", fileName);

            RestClient client = DefaultClient;
            IRestResponse<Response<FilePrepareStatus>> response = client.Execute<Response<FilePrepareStatus>>(request);
            response.EnsureSucceeded();
            return response;
        }

        public static IRestResponse<Response<object>> StartDownload(string type, string fileName) {
            Uri url = new Uri(Host, "/api/v1/mirror/start-download");
            IRestRequest request = new RestRequest(url, Method.POST)
                .AddQueryParameter("type", type)
                .AddQueryParameter("fileName", fileName);

            RestClient client = DefaultClient;
            IRestResponse<Response<object>> response = client.Execute<Response<object>>(request);
            response.EnsureSucceeded();
            return response;
        }

        public static void EnsureSucceeded(this IRestResponse response) {
            if (response.ErrorException is { } e) {
                throw e;
            }
            if (response.ResponseStatus != ResponseStatus.Completed) {
                throw response.ResponseStatus.ToWebException();
            }
            if (!response.IsSuccessful) {
                throw new HttpException((int)response.StatusCode, response.StatusDescription);
            }
        }

        public static void EnsureSucceeded<T>(this IRestResponse<Response<T>> response) {
            ((IRestResponse)response).EnsureSucceeded();
            if (response.Data.Code != 200) {
                throw new ServerException(response.Data.Code ?? -1, response.Data.Message);
            }
        }

    }
}
