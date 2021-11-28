using System;

namespace Celeste.Mod.ChinaMirror.Endpoints {
    public class ServerException : Exception {

        public int Code { get; }

        public string ServerMessage { get; }

        public ServerException(int code, string serverMessage) : base($"{code} - {serverMessage}") {
            Code = code;
            ServerMessage = serverMessage;
        }

    }
}
