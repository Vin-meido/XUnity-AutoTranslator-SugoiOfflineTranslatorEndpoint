using System;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using UnityEngine.Networking;
using System.Reflection;
using System.IO;
using XUnity.AutoTranslator.Plugin.Core;
using XUnity.AutoTranslator.Plugin.Core.Web;
using XUnity.Common.Logging;

using SugoiOfflineTranslator.SimpleJSON;

namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : ITranslateEndpoint, IDisposable, IMonoBehaviour_Update
    {
        public string Id => "SugoiOfflineTranslator";

        public string FriendlyName => "Sugoi offline translator endpoint";

        public int MaxConcurrency => 1;
        public int MaxTranslationsPerRequest { get; set; } = 100;

        private Process process;
        private bool isDisposing = false;
        private bool isStarted = false;

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

        public void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");

            this.SugoiInstallPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "InstallPath", "");
            this.ServerPort = context.GetOrCreateSetting("SugoiOfflineTranslator", "ServerPort", "14367");
            this.EnableCuda = context.GetOrCreateSetting("SugoiOfflineTranslator", "EnableCuda", false);
            this.MaxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
            this.ServerScriptPath = context.GetOrCreateSetting("SugoiOfflineTranslator", "CustonServerScriptPath", "");
            this.MaxTranslationsPerRequest = context.GetOrCreateSetting("SugoiOfflineTranslator", "MaxBatchSize", 10);
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

                this.process.OutputDataReceived += (sender, args) =>
                {
                    if (this.LogServerMessages) {
                    XuaLogger.AutoTranslator.Info(args.Data);
                    }
                };

                this.process.ErrorDataReceived += (sender, args) =>
                {
                    if (this.LogServerMessages)
                    {
                    XuaLogger.AutoTranslator.Info(args.Data);
                    }
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
            var json = new JSONObject();
            json["content"] = "";
            json["message"] = "close server";
            
            var data = json.ToString();

            var request = this.CreateRequest(data);
            request.Send();
        }

        private UnityWebRequest CreateRequest(string data_str)
        {
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
            var json = new JSONObject();
            json["content"] = context.UntranslatedText;
            json["batch"] = context.UntranslatedTexts;
            json["message"] = "translate batch";
            var data = json.ToString();

            var request = CreateRequest(data);

            yield return request.Send();

            if (request.responseCode == 200)
            {
                var handler = request.downloadHandler;
                var response = handler.text;
                var translations = JSON.Parse(response).AsStringList.ToArray();
                context.Complete(translations);

            }
        }
    }
}
