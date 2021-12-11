using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Net;
using XUnity.AutoTranslator.Plugin.Core.Web;

namespace SugoiOfflineTranslator.HttpWorker
{
    class XUATHttpWorker : HttpWorker
    {
        string result;
        public override string Result => result;

        public override IEnumerator Post(string url, string body)
        {

            var request = new XUnityWebRequest("POST", url, body);
            request.Headers[HttpRequestHeader.ContentType] = "application/json";
            XUnityWebClient xunityWebClient = new XUnityWebClient();
            XUnityWebResponse response = xunityWebClient.Send(request);

            var iterator = response.GetSupportedEnumerator();
            while (iterator.MoveNext()) yield return iterator.Current;

            if(response.IsTimedOut || response.Code != HttpStatusCode.OK)
            {
                yield break;
            }

            result = response.Data;
        }
    }
}
