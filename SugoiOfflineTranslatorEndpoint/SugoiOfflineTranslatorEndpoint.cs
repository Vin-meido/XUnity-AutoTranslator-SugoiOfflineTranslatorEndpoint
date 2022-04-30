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

        private string ServerExecPath { get; set; }

        //private string ServerPort => 14366;
        private string ServerPort { get; set; }

        //private string SugoiInstallPath => "G:\\Downloads\\Sugoi-Japanese-Translator-V3.0";
        private string SugoiInstallPath { get; set; }
        
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

            this.EnableCTranslate2 = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableCTranslate2", false);


            if (string.IsNullOrEmpty(this.SugoiInstallPath))
            {
                throw new Exception("need to specify InstallPath");
            }

            var pythonExePathCandidates = new string[]
            {
                Path.Combine(this.SugoiInstallPath, "Power-Source\\Python38\\python.exe"),
                Path.Combine(this.SugoiInstallPath, "Power-Source\\Python39\\python.exe"),
            };

            this.PythonExePath = pythonExePathCandidates.Where(p => File.Exists(p)).FirstOrDefault();
            if (string.IsNullOrEmpty(this.PythonExePath))
            {
                throw new Exception("unable to find python power source (Python3x folder)");
            }

            var pythonServerExecPathCandidates = new string[]
            {
                Path.Combine(this.SugoiInstallPath, "backendServer\\Program-Backend\\Sugoi-Translator-Offline\\offlineTranslation"),
                Path.Combine(this.SugoiInstallPath, "backendServer\\Program-Backend\\Sugoi-Japanese-Translator\\offlineTranslation")
            };
            this.ServerExecPath = pythonServerExecPathCandidates.Where(p => Directory.Exists(p)).FirstOrDefault();
            if (string.IsNullOrEmpty(this.ServerExecPath))
            {
                throw new Exception("unable to find exec path (offlineTranslation folder)");
            }


            if (string.IsNullOrEmpty(this.ServerScriptPath))
            {
                var tempPath = Path.GetTempPath();
                this.ServerScriptPath = Path.Combine(tempPath, "SugoiOfflineTranslatorServer.py");
                File.WriteAllBytes(this.ServerScriptPath, Properties.Resources.SugoiOfflineTranslatorServer);
            }

            if (this.EnableShortDelay)
            {
                context.SetTranslationDelay(0.1f);
            }

            if (this.DisableSpamChecks)
            {
                context.DisableSpamChecks();
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
                string ctranslate = this.EnableCTranslate2 ? "--ctranslate2" : "";

                XuaLogger.AutoTranslator.Info($"Running Sugoi Offline Translation server:\n\tExecPath: {this.ServerExecPath}\n\tPythonPath: {this.PythonExePath}\n\tScriptPath: {this.ServerScriptPath}");

                this.process = new Process();
                this.process.StartInfo = new ProcessStartInfo()
                {
                    FileName = this.PythonExePath,
                    Arguments = $"\"{this.ServerScriptPath}\" {this.ServerPort} {cuda} {ctranslate}",
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
            yield return base.Translate(context);
            var elapsed = stopwatch.Elapsed.TotalSeconds;

            if(LogServerMessages)
            {
                XuaLogger.AutoTranslator.Info($"Translate complete {elapsed}s");

            }
        }

        public override IEnumerator OnBeforeTranslate(IHttpTranslationContext context)
        {
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
            json["content"] = context.UntranslatedText;
            json["batch"] = context.UntranslatedTexts;
            json["message"] = "translate batch";
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
            context.Complete(result.AsStringList.ToArray());
        }
    }
}
