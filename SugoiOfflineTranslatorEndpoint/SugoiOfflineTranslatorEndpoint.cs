using System;
using System.Linq;
using System.Collections;
using System.Text;
using System.Diagnostics;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;
using System.Reflection;
using System.IO;
using System.Net;
using XUnity.AutoTranslator.Plugin.Core;
using XUnity.Common.Logging;
using SugoiOfflineTranslator.SimpleJSON;

namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : HttpEndpoint, ITranslateEndpoint, IDisposable
    {
        public override string Id => "SugoiOfflineTranslator";

        public override string FriendlyName => "Sugoi Offline Translator";

        //public override int MaxConcurrency => 1;
        int maxTranslationsPerRequest { get; set; } = 100;

        public override int MaxTranslationsPerRequest => maxTranslationsPerRequest;

        private Process process;
        private bool isDisposing = false;
        private bool isStarted = false;
        private bool isReady = false;

        private string ServerScriptPath { get; set; }
        private byte[] ServerScriptData { get; set; }

        private string ServerExecPath { get; set; }

        //private string ServerPort => 14366;
        private string ServerPort { get; set; }

        private string SugoiInstallPath { get; set; }

        private bool UseExternalServer { get; set; } = false;

        private bool EnableCuda { get; set; }

        private bool EnableShortDelay { get; set; }

        private bool DisableSpamChecks { get; set; }

        private bool LogServerMessages { get; set; }

        private bool EnableCTranslate2 { get; set; }

        private string PythonExePath { get; set; }

        public override void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");

            this.SugoiInstallPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "InstallPath", "");
            this.ServerPort = context.GetOrCreateSetting("SugoiOfflineTranslator", "ServerPort", "14367");
            this.EnableCuda = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableCuda", false);
            this.maxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
            this.ServerScriptPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "CustomServerScriptPath", "");
            this.maxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
            this.EnableShortDelay = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableShortDelay", false);
            this.DisableSpamChecks = context.GetOrCreateSetting("SugoiOfflineTranslator", "DisableSpamChecks", true);
            this.LogServerMessages = context.GetOrCreateSetting("SugoiOfflineTranslator", "LogServerMessages", false);

            if (this.EnableShortDelay)
            {
                context.SetTranslationDelay(0.1f);
            }

            if (this.DisableSpamChecks)
            {
                context.DisableSpamChecks();
            }

            if (!string.IsNullOrEmpty(this.SugoiInstallPath))
            {
                this.SetupServer(context);
            }
            else
            {
                XuaLogger.AutoTranslator.Info($"Sugoi install path not configured. Either configure a path or start sugoi externally.");
                this.UseExternalServer = true;
                this.ServerPort = "14366";
                this.maxTranslationsPerRequest = 1;
            }
        }

        private static string PathCandidate(string description, string prefix, string[] paths, bool optional=false)
        {
            string[] candidates = paths.Select(p => Path.Combine(prefix, p)).ToArray();
            var existing = candidates.Where(p => File.Exists(p) || Directory.Exists(p)).FirstOrDefault();
            if (existing != null)
            {
                return existing;
            }
            else
            {
                if (optional) return null;
                throw new Exception($"Unable to find {description} at any of the following locations: {string.Join(", ", candidates)}");
            }
        }

        private void SetupServer(IInitializationContext context)
        {
            this.PythonExePath = PathCandidate(
                "Python power source",
                this.SugoiInstallPath,
                new string[] { "Code\\Power-Source\\Python39\\python.exe" });

            this.ServerExecPath = PathCandidate(
                "Server exec path",
                this.SugoiInstallPath,
                new string[]
                {
                    // V11
                    "Code\\backendServer\\Modules\\Translation-API-Server\\Offline",

                    // V10
                    "Code\\backendServer\\Program-Backend\\Sugoi-Japanese-Translator\\offlineTranslation"
                });

            if (string.IsNullOrEmpty(this.ServerScriptPath))
            {
                this.ServerScriptData = Properties.Resources.SugoiOfflineTranslatorServer;
            }
            else
            {
                this.ServerScriptData = File.ReadAllBytes(this.ServerScriptPath);
            }

            var configuredEndpoint = context.GetOrCreateSetting<string>("Service", "Endpoint");
            if (configuredEndpoint == this.Id)
            {
                this.StartProcess();
            }
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
                string cuda = this.EnableCuda ? "--cuda" : "";

                XuaLogger.AutoTranslator.Info($"Running Sugoi Offline Translation server:\n\tExecPath: {this.ServerExecPath}\n\tPythonPath: {this.PythonExePath}\n\tCustomScriptPath: {this.ServerScriptPath}");

                this.process = new Process();
                this.process.StartInfo = new ProcessStartInfo()
                {
                    FileName = this.PythonExePath,
                    Arguments = $" - {this.ServerPort} {cuda}",
                    WorkingDirectory = this.ServerExecPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                };

                this.process.OutputDataReceived += this.ServerDataReceivedEventHandler;
                this.process.ErrorDataReceived += this.ServerDataReceivedEventHandler;

                this.process.Start();

                using (var stream = this.process.StandardInput.BaseStream)
                {
                    stream.Write(this.ServerScriptData, 0, this.ServerScriptData.Length);
                }

                this.process.BeginErrorReadLine();
                this.process.BeginOutputReadLine();
                this.isStarted = true;


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

        IEnumerator ITranslateEndpoint.Translate(ITranslationContext context)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var iterator = base.Translate(context);

            while (iterator.MoveNext()) yield return iterator.Current;
            
            var elapsed = stopwatch.Elapsed.TotalSeconds;

            if(LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info($"Translate complete {elapsed}s");
            }
        }

        public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
        {
            if (this.UseExternalServer)
            {
                yield break;
            }

            if (this.isStarted && this.process.HasExited)
            {
                this.isStarted = false;
                this.isReady = false;

                XuaLogger.AutoTranslator.Warn($"Translator server process exited unexpectedly [status {process.ExitCode}]");
            }

            if (!this.isStarted && !this.isDisposing)
            {
                XuaLogger.AutoTranslator.Warn($"Translator server process not running. Starting...");
                this.StartProcess();
            }

            while (!isReady) yield return null;
        }

        public string GetUrlEndpoint()
        {
            return $"http://127.0.0.1:{ServerPort}/";
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var json = new JSONObject();

            if (!this.UseExternalServer)
            {
                json["content"] = context.UntranslatedText;
                json["batch"] = context.UntranslatedTexts;
                json["message"] = "translate batch";
            }
            else
            {
                json["content"] = context.UntranslatedText;
                json["message"] = "translate sentences";
            }

            var data = json.ToString();

            var request = new XUnityWebRequest("POST", GetUrlEndpoint(), data);
            request.Headers["Content-Type"] = "application/json";
            request.Headers["Accept"] = "*/*";

            context.Complete(request);
        }

        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            var data = context.Response.Data;
            var result = JSON.Parse(data);
            
            if (!UseExternalServer)
            {
                context.Complete(result.AsStringList.ToArray());
            }
            else
            {
                if (result.IsString)
                {
                    context.Complete(result.Value);
                }
                else
                {
                    context.Fail($"Unexpected return from server: {data}");
                }
            }
        }
    }
}
