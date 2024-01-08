using OAS.Network;
using OAS.Util;
using OAS.Util.Logging;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace xtender {

    internal class NetworkManager {

        public const string TAG = "Network";

        private static string Host;

        internal static void SetHost(string host) {
            Log.WriteTraced("Updating GS url to " + host);
            Host = host;
        }

        internal static string Request(string url, string request_content) {

            string full_url = "https://" + Host + "/sync_v01r03/chassis/xtender/" + url;
            Log.WriteTraced("Starting network request to " + full_url);
            Log.Write("Request body: " + request_content, "Debug");
            if (request_content == null) {
                request_content = "";
            }

            try {

                HttpWebRequest http = WebRequest.CreateHttp(full_url);

                byte[] data = Encoding.UTF8.GetBytes(request_content);

                http.Method = "POST";
                http.ContentLength = data.Length;
                http.UserAgent = "xtender/" + Assembly.GetExecutingAssembly().GetName().Version;
                http.Accept = "application/json";
                http.ContentType = "application/json";
                http.Headers.Add(HttpRequestHeader.AcceptEncoding, "utf-8");
                http.GetRequestStream().Write(data, 0, data.Length);

                HttpWebResponse response = (HttpWebResponse)http.GetResponse();

                if (response.StatusCode != HttpStatusCode.OK) {
                    throw new WebException("Server returned status " + response.StatusCode + " - " + response.StatusDescription);
                }

                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) {
                    return reader.ReadToEnd();
                }

            } catch (Exception ex) {
                Log.WriteFault(ex, "Network request failure: " + full_url, TAG);
                return "error:network request failure - " + ex.Message;
            } finally {
                Log.WriteTraced("Finished network request");
            }

        }

        internal static void RequestFile(string url, string path) {
            url = "http://" + url;
            Log.Write("Downloading: " + url);
            NetworkRequest nr = new NetworkRequest() {
                Url = url,
                TargetFile = path,
                ThrowOnHTTPError = true
            };
            nr.Execute();
        }
    }
}