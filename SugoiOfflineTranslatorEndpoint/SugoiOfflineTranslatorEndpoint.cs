using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using UnityEngine.Networking;
using System.Reflection;
using System.IO;
using XUnity.AutoTranslator.Plugin.Core;
using XUnity.Common.Logging;

namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : ITranslateEndpoint, IDisposable, IMonoBehaviour_Update
    {
        public string Id => "SugoiOfflineTranslatorEndpoint";

        public string FriendlyName => "Sugoi offline translator endpoint";

        public int MaxConcurrency => 1;
        public int MaxTranslationsPerRequest { get; set; } = 100;

        private Process process;
        private bool isDisposing = false;
        private bool isStarted = false;

        private string AssemblyDirectory {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);

            }
        }

        private string ServerScriptPath
        {
            get
            {
                return Path.Combine(this.AssemblyDirectory, "SugoiOfflineTranslatorServer.py");
            }
        }

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

        private string PythonExePath
        {
            get
            {
                return Path.Combine(this.SugoiInstallPath, "Power-Source\\Python38\\python.exe");
            }
        }

        public void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");

            this.SugoiInstallPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "InstallPath", "");
            this.ServerPort = context.GetOrCreateSetting("SugoiOfflineTranslator", "ServerPort", "14367");
            this.EnableCuda = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableCuda", false);
            this.MaxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);

            if (string.IsNullOrEmpty(this.SugoiInstallPath))
            {
                throw new Exception("need to specify InstallPath");
            }

            this.StartProcess();
        }

        public void Dispose()
        {
            this.isDisposing = true;
            if (this.process != null)
            {
                this.SendShutdown();
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

                this.process = new Process();
                this.process.StartInfo = new ProcessStartInfo()
                {
                    FileName = this.PythonExePath,
                    Arguments = $"{this.ServerScriptPath} {this.ServerPort} {cuda}",
                    WorkingDirectory = this.ServerExecPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                this.process.OutputDataReceived += (sender, args) =>
                {
                    XuaLogger.AutoTranslator.Debug(args.Data);
                };

                this.process.ErrorDataReceived += (sender, args) =>
                {
                    XuaLogger.AutoTranslator.Debug(args.Data);
                };

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

        private void SendShutdown()
        {
            var data = new Dictionary<string, object>()
            {
                { "content", "" },
                { "message", "close server" }
            };

            var request = this.CreateRequest(data);
            request.Send();
        }

        private UnityWebRequest CreateRequest(Dictionary<string, object> data)
        {
            var data_str = JsonConvert.SerializeObject(data);

            var request = new UnityWebRequest(
                $"http://localhost:{ServerPort}/",
                "POST",
                new DownloadHandlerBuffer(),
                new UploadHandlerRaw(Encoding.UTF8.GetBytes(data_str)));

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "*/*");
            return request;
        }

        public IEnumerator Translate(ITranslationContext context)
        {
            var data = new Dictionary<string, object>()
            {
                { "content", context.UntranslatedText },
                { "batch", context.UntranslatedTexts },
                { "message", "translate batch" }
            };

            var request = CreateRequest(data);

            yield return request.Send();

            if (request.responseCode == 200)
            {
                var handler = request.downloadHandler;
                var response = handler.text;
                var translations = JsonConvert.DeserializeObject<string[]>(response);
                context.Complete(translations);

            }
        }
    }
}
