using System;
using System.Net;
using System.Collections;
using System.IO;
using System.Threading;
using XUnity.Common.Logging;

namespace SugoiOfflineTranslator.HttpWorker
{
    class HttpRequestWorker : HttpWorker
    {
        string result;
        bool complete = false;
        public override string Result => result;

        public override IEnumerator Post(string url, string body)
        {
            ThreadPool.QueueUserWorkItem(o => {
                try
                {
                    result = PostTask(url, body);
                }
                catch(Exception e)
                {
                    XuaLogger.AutoTranslator.Error($"Unexpected exception occured:\n {e.StackTrace}");
                }
                finally
                {
                    complete = true;
                }
                XuaLogger.AutoTranslator.Debug($"Post to {url} complete");
            });

            while(!complete)
            {
                yield return null;
            }
        }

        static string PostTask(string url, string body)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/json";

            using (var requestStream = request.GetRequestStream())
            {
                using (var writer = new StreamWriter(requestStream))
                {
                    writer.Write(body);
                }
            }

            var response = request.GetResponse() as HttpWebResponse;
            string tempResult;

            using (var responseStream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(responseStream))
                {
                    tempResult = reader.ReadToEnd();
                }
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return tempResult;
            }

            return "";
        }
    }
}
