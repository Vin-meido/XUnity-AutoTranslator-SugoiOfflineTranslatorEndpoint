using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using SimpleJSON;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Http;
using XUnity.AutoTranslator.Plugin.Core.Web;

namespace SugoiOfflineTranslator
{
    public class SugoiOfflineTranslatorEndpoint : HttpEndpoint
    {
        public override string Id => "SugoiOfflineTranslatorEndpoint";

        public override string FriendlyName => "Sugoi offline translator endpoint";

        public override int MaxConcurrency => 100;
        public override int MaxTranslationsPerRequest => 100;

        public override void Initialize(IInitializationContext context)
        {
            if (context.SourceLanguage != "ja") throw new Exception("Only ja is supported as source language");
            if (context.DestinationLanguage != "en") throw new Exception("Only en is supported as destination language");
        }

        public override void OnCreateRequest(IHttpRequestCreationContext context)
        {
            var data = new Dictionary<string, string>()
            {
                { "content", context.UntranslatedText },
                { "message", "translate sentences" }
            };
            
            var data_str = JsonConvert.SerializeObject(data);

            var request = new XUnityWebRequest("POST", "http://localhost:14366/", data_str);
            request.Headers[HttpRequestHeader.ContentType] = "application/json";
            request.Headers[HttpRequestHeader.Accept] = "*/*";
            request.Headers[HttpRequestHeader.AcceptCharset] = "UTF-8";

            context.Complete(request);
        }

        public override void OnExtractTranslation(IHttpTranslationExtractionContext context)
        {
            var data = context.Response.Data;
            //var obj = JSON.Parse(data);
            var translation = JsonConvert.DeserializeObject(data) as string;
            //var code = obj.AsObject["code"].ToString();
            //if (code != "200") context.Fail("Received bad response code: " + code);

            //var token = obj.AsObject["text"].ToString();
            //var translation = JsonHelper.Unescape(token.Substring(2, token.Length - 4));

            if (string.IsNullOrEmpty(translation)) context.Fail("Received no translation.");

            context.Complete(translation);
        }
    }
}
