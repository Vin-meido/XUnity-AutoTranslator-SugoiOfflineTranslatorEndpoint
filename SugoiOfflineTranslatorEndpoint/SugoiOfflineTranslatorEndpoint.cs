using System;
using System.Collections;
using System.Diagnostics;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using System.IO;
using XUnity.AutoTranslator.Plugin.Core;
using XUnity.Common.Logging;
using SugoiOfflineTranslator.SimpleJSON;
using SugoiOfflineTranslator.HttpWorker;


namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : ITranslateEndpoint, IDisposable
    {
        public string Id => "SugoiOfflineTranslator";

        public string FriendlyName => "Sugoi offline translator endpoint";

        public int MaxConcurrency => 1;

        int maxTranslationsPerRequest { get; set; } = 100;

        public int MaxTranslationsPerRequest => maxTranslationsPerRequest;

        private Process process;
        private bool isDisposing = false;
        private bool isStarted = false;
        private bool isReady = false;

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

        public void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");

            this.SugoiInstallPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "InstallPath", "");
            this.ServerPort = context.GetOrCreateSetting("SugoiOfflineTranslator", "ServerPort", "14367");
            this.EnableCuda = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableCuda", false);
            this.maxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
            this.ServerScriptPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "CustomServerScriptPath", "");
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

                XuaLogger.AutoTranslator.Info($"Running Sugoi Offline Translation server:\n\tExecPath: {this.ServerExecPath}\n\tPythonPath: {this.PythonExePath}\n\tScriptPath: {this.ServerScriptPath}");

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

        /*
        public void Update()
        {
            return;
            if (this.isStarted && !this.isDisposing)
            {
                if(this.process.HasExited)
                {
                    XuaLogger.AutoTranslator.Error($"Translator server process exited unexpectedly [status {process.ExitCode}]");
                    this.isStarted = false;
                }
            }
        }*/

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

        
        public IEnumerator OnBeforeTranslate(ITranslationContext context)
        {
            if (this.process == null)
            {
                this.StartProcess();
            }

            while (!isReady) yield return null;
        }
        
        public string GetUrlEndpoint()
        {
            var ts = DateTime.Now.Ticks;
            return $"http://127.0.0.1:{ServerPort}/?ts={ts}";
        }

        public string CreateRequestBody(ITranslationContext context)
        {
            var json = new JSONObject();
            json["content"] = context.UntranslatedText;
            json["batch"] = context.UntranslatedTexts;
            json["message"] = "translate batch";
            //json["message"] = "translate sentences";
            return json.ToString();
        }

        public IEnumerator Translate(ITranslationContext context)
        {
            IEnumerator setup = this.OnBeforeTranslate(context);
            if (setup != null)
            {
                while (setup.MoveNext())
                {
                    object obj = setup.Current;
                    yield return obj;
                }
            }


            var url = GetUrlEndpoint();
            var body = CreateRequestBody(context);
            var worker = new HttpRequestWorker();
            IEnumerator iterator = worker.Post(url, body);

            while (iterator.MoveNext()) yield return iterator.Current;

            if (!string.IsNullOrEmpty(worker.Result))
            {
                var result = JSON.Parse(worker.Result);
                var resultArray = result.AsStringList.ToArray();

                context.Complete(resultArray);
            }
        }
    }


}
