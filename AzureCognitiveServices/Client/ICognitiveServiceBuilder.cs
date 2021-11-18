using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureCognitiveServices.Client
{
    public interface ICognitiveServiceBuilder
    {
        public CognitiveServiceBuilder HavingHttpConnection(Action<IHttpConnectedServiceBuilder> connectionConfigure);
    }
}
