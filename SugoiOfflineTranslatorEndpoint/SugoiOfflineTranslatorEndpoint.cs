using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using UnityEngine.Networking;


namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : ITranslateEndpoint
    {
        public string Id => "SugoiOfflineTranslatorEndpoint";

        public string FriendlyName => "Sugoi offline translator endpoint";

        public int MaxConcurrency => 1;
        public int MaxTranslationsPerRequest => 100;

        public void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");
        }

        public IEnumerator Translate(ITranslationContext context)
        {
            var data = new Dictionary<string, object>()
            {
                { "content", context.UntranslatedText },
                { "batch", context.UntranslatedTexts },
                { "message", "translate batch" }
            };

            
            var data_str = JsonConvert.SerializeObject(data);

            var request = new UnityWebRequest(
                "http://localhost:14366/",
                "POST",
                new DownloadHandlerBuffer(),
                new UploadHandlerRaw(Encoding.UTF8.GetBytes(data_str)));

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "*/*");

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
