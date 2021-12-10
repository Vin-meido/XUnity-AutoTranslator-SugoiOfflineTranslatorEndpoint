using System;
using System.Collections;
using System.Text;
using System.Diagnostics;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using System.Reflection;
using System.IO;
using XUnity.AutoTranslator.Plugin.Core;
using XUnity.Common.Logging;
using SugoiOfflineTranslator.SimpleJSON;

namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : HttpEndpoint, ITranslateEndpoint, IDisposable, IMonoBehaviour_Update
    {
        public override string Id => "SugoiOfflineTranslator";

        public override string FriendlyName => "Sugoi offline translator endpoint";

        //public override int MaxConcurrency => 1;
        int maxTranslationsPerRequest { get; set; } = 100;

        public override int MaxTranslationsPerRequest => maxTranslationsPerRequest;

        private Process process;
        private bool isDisposing = false;
        private bool isStarted = false;
        private bool isReady = false;

        private string TranslatorPath
        {
            get
            {
                var assembly = typeof(AutoTranslationPlugin).Assembly;
                var type = assembly.GetType("XUnity.AutoTranslator.Plugin.Core.Configuration.Settings");
                if (type == null)
                {
                    XuaLogger.AutoTranslator.Error("Cannot load xuat settings class");
            }

                var prop = type.GetField("TranslatorsPath", BindingFlags.Static | BindingFlags.Public);
                if (prop == null)
        {
                    XuaLogger.AutoTranslator.Error("Cannot get translator path");
            }

                return prop.GetValue(null) as string;
        }
        }

        private string ServerScriptPath { get; set; }

        private string ServerExecPath
        {
            get {
                return Path.Combine(this.SugoiInstallPath, "backendServer\\Program-Backend\\Sugoi-Translator-Offline\\offlineTranslation");
            }
        }

        //private string ServerPort => 14366;
        private string ServerPort { get; set; }

        //private string SugoiInstallPath => "G:\\Downloads\\Sugoi-Japanese-Translator-V3.0";
        private string SugoiInstallPath { get; set; }
        
        private bool EnableCuda { get; set; }

        private bool LogServerMessages { get; set; }

        private string PythonExePath
        {
            get
            {
                return Path.Combine(this.SugoiInstallPath, "Power-Source\\Python38\\python.exe");
            }
        }

        public override void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");

            this.SugoiInstallPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "InstallPath", "");
            this.ServerPort = context.GetOrCreateSetting("SugoiOfflineTranslator", "ServerPort", "14367");
            this.EnableCuda = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableCuda", false);
            this.maxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
            this.ServerScriptPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "CustonServerScriptPath", "");
            this.maxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
            this.LogServerMessages = context.GetOrCreateSetting("SugoiOfflineTranslator", "LogServerMessages", false);

            if (string.IsNullOrEmpty(this.SugoiInstallPath))
            {
                throw new Exception("need to specify InstallPath");
            }

            if (string.IsNullOrEmpty(this.ServerScriptPath))
            {
                var tempPath = Path.GetTempPath();
                this.ServerScriptPath = Path.Combine(tempPath, "SugoiOfflineTranslatorServer.py");
                File.WriteAllBytes(this.ServerScriptPath, Properties.Resources.SugoiOfflineTranslatorServer);
            }

            this.StartProcess();
        }

        public void Dispose()
        {
            this.isDisposing = true;
            if (this.process != null)
            {
                this.process.Kill();
                this.process.Dispose();
                this.process = null;
            }
        }

        private void StartProcess()
        {
            if (this.process == null || this.process.HasExited)
            {
                string cuda = this.EnableCuda ? "cuda" : "nocuda";

                XuaLogger.AutoTranslator.Info($"Running Sugoi Offline Translation server:\n\tTranslatorsDir: {this.TranslatorPath}\n\tExecPath: {this.ServerExecPath}\n\tPythonPath: {this.PythonExePath}\n\tScriptPath: {this.ServerScriptPath}");

                this.process = new Process();
                this.process.StartInfo = new ProcessStartInfo()
                {
                    FileName = this.PythonExePath,
                    Arguments = $"\"{this.ServerScriptPath}\" {this.ServerPort} {cuda}",
                    WorkingDirectory = this.ServerExecPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                this.process.OutputDataReceived += this.ServerDataReceivedEventHandler;
                this.process.ErrorDataReceived += this.ServerDataReceivedEventHandler;

                this.process.Start();
                this.process.BeginErrorReadLine();
                this.process.BeginOutputReadLine();
                this.isStarted = true;
            }
        }

        public void Update()
        {
            if (this.isStarted && !this.isDisposing)
            {
                if(this.process.HasExited)
                {
                    XuaLogger.AutoTranslator.Error($"Translator server process exited unexpectedly [status {process.ExitCode}]");
                    this.isStarted = false;
                }
            }
        }

        void ServerDataReceivedEventHandler(object sender, DataReceivedEventArgs args)
        {
            if (this.LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info(args.Data);
            }

            if (!this.isReady && args.Data.Contains("(Press CTRL+C to quit)"))
            {
                this.isReady = true;
            }
        }

        /*
        private void SendShutdown()
        {
            var json = new JSONObject();
            json["content"] = "";
            json["message"] = "close server";

            var data = json.ToString();

            var request = this.CreateRequest(data);
            request.Send();
        }
        */

        IEnumerator ITranslateEndpoint.Translate(ITranslationContext context)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            yield return base.Translate(context);
            var elapsed = stopwatch.Elapsed.TotalSeconds;

            if(LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info($"Translate complete {elapsed}s");

            }
        }

        public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
        {
            while (!isReady) yield return null;
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var json = new JSONObject();
            json["content"] = context.UntranslatedText;
            json["batch"] = context.UntranslatedTexts;
            json["message"] = "translate batch";
            var data = json.ToString();

            var request = new XUnityWebRequest("POST", $"http://127.0.0.1:{ServerPort}/", data);
            request.Headers["Content-Type"] = "application/json";
            request.Headers["Accept"] = "*/*";

            context.Complete(request);
        }

        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            var data = context.Response.Data;
            var result = JSON.Parse(data);
            context.Complete(result.AsStringList.ToArray());
        }
    }
}
