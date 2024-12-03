using CommandLine;
using OAS.Util;
using OAS.Util.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Threading;

namespace xtender {

    public class Program {

        public const int XTENDER_PROTOCOL_VERSION = 1;
        public static readonly String VERSION = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public const String PIPE_NAME = "xtal-xtender";

        public const String RESPONSE_OK = "ok";
        public const String ERROR_DELAYING = "error:delaying";
        public const String ERROR_DELAYED = "error:delayed";
        public const String ERROR_UNKNOWN_COMMAND = "error:unknown command";
        public const String ERROR_EXCEPTION = "error:exception thrown";
        public const String ERROR_VERSION = "error:xtender protocol version mismatch";

        private static Action ExecutorDelayFunction;
        private static String xtal_client;
        private static String DelayResponse;
        public class Options {
            [Option('q', "quiet", Required = false, HelpText = "Output nothing.")]
            public bool Quiet { get; set; }

            [Option("validate-certificates", Required = false, HelpText = "Validate the server's SSL certificate on a request.")]
            public bool ValidateCertificates { get; set; }

        }

        static void RunOptions(Options opts) {
            o = opts;
        }

        public static Options o;

        public static int Main(string[] args) {
            Parser.Default.ParseArguments<Options>(args).WithParsed(RunOptions);
            if (o == null) {
                return 1;
            }

            Console.Title = "xtender " + VERSION;
            if (!o.Quiet) {
                Log.Init(false, 0);
            }
            Log.Write("xtender " + VERSION);
            
            new Thread(ExecutorThread).Start();

            if (!o.ValidateCertificates) {
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });
            } else {
                Log.WriteWarning("Server certificate validation is ENABLED!");
            }

            Log.Write("Started up");
            while (true) {
                Log.Write("Opening pipe server...", "Debug");
                NamedPipeServerStream server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.InOut, 5);
                Log.Write("Opened pipe server", "Debug");
                server.WaitForConnection();
                Log.Write("Got connection", "Debug");
                StreamReader reader = new StreamReader(server);
                StreamWriter writer = new StreamWriter(server);

                try {
                    do {
                        String str = reader.ReadLine();
                        Log.Write("Received: " + str, "Debug");
                        if (str != null) {
                            string @out = Parse(str);
                            Log.Write("Response: " + @out, "Debug");
                            writer.Write(@out);
                            writer.Flush();
                            writer.Close();
                        }
                    } while (server.IsConnected);

                }catch(Exception ex) {
                    Log.WriteFault(ex, "Error reading from pipe");
                } finally {
                    server.Close();
                }
            }
        }

        private static void ExecutorThread() {
            Log.Write("Executor started", "Debug");
            while (true) {
                if (ExecutorDelayFunction != null) {
                    try {
                        ExecutorDelayFunction();
                    }catch(Exception ex) {
                        Log.WriteFault(ex, "Delayed function error");
                        SetDelayedResponse(ERROR_EXCEPTION);
                    }
                }
                Thread.Sleep(50);
            }
            Log.WriteError("Executor ended", "Debug");
        }

        private static String Parse(string str) {

            try {
                String[] args = str.Split(new char[]{ ':' }, 3);

                String cmd = args[0];
                int strip = cmd.IndexOf('~');
                if (strip > 0) {
                    cmd = cmd.Substring(0, strip);
                }

                switch (cmd) {
                    case "alive":
                        return RESPONSE_OK;
                    case "preq":
                        if (DelayResponse != null) {
                            ExecutorDelayFunction = null;
                            return DelayResponse;
                        } else if (ExecutorDelayFunction == null) {
                            return "error:no pending request";
                        } else {
                            return ERROR_DELAYED;
                        }
                    case "get_version":
                        return VERSION;
                    case "check_protocol_version":
                        return Int32.Parse(args[1]) == XTENDER_PROTOCOL_VERSION ? RESPONSE_OK : ERROR_VERSION;
                    case "set_xtal_version":
                        xtal_client = args[1];
                        Log.Write("xtal client announcement: " + args[1] + ", version: " + args[2]);
                        return RESPONSE_OK;
                    case "set_gs_host":
                        NetworkManager.SetHost(String.Join(":", args, 1, args.Length - 1));
                        return RESPONSE_OK;
                    case "network_request":
                        return GetRemoteData(args[1], args[2]);
                    case "download":
                        return Download(args[1], args[2]);
                    case "exit_xtal":
                        CloseProcess();
                        return RESPONSE_OK;
                    default:
                        return ERROR_UNKNOWN_COMMAND;
                }
            } catch (Exception ex) {
                Log.WriteFault(ex, "Failed to parse request: " + str);
                return ERROR_EXCEPTION;
            }

        }

        private static void CloseProcess() {
            Log.Write("CloseProcess");
            foreach (Process p in Process.GetProcessesByName(xtal_client)) {
                Log.Write("Closing: " + p.ProcessName+"/"+p.Id);
                p.Close();
            }
        }

        private static void StartDelayed(Action func) {
            Log.Write("StartDelayed: " + func, "Debug");
            DelayResponse = null;
            ExecutorDelayFunction = func;
        }

        private static string GetRemoteData(string url, string request) {
            StartDelayed(() => {
                string resp = NetworkManager.Request(url, request);
                SetDelayedResponse(resp);
            });
            return ERROR_DELAYING;
        }

        private static string Download(string url, string file) {
            string path = "../data/" + file;
            if (File.Exists(file)) {
                return RESPONSE_OK;
            } else {
                StartDelayed(() => {
                    NetworkManager.RequestFile(url, path);
                    SetDelayedResponse(RESPONSE_OK);
                });
                return ERROR_DELAYING;
            }
        }

        public static void SetDelayedResponse(string str) {
            Log.Write("SetDelayedResponse: " + str, "Debug");
            DelayResponse = str;
            ExecutorDelayFunction = null;
        }
    }
}
