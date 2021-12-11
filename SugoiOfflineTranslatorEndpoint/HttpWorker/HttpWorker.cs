using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace SugoiOfflineTranslator.HttpWorker
{
    abstract class HttpWorker
    {
        public abstract IEnumerator Post(string url, string body);

        public abstract string Result { get; }
    }
}
