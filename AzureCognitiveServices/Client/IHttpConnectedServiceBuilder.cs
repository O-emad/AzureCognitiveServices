using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureCognitiveServices.Client
{
    public interface IHttpConnectedServiceBuilder
    {
        public IHttpConnectedServiceBuilder WithServiceKey(string key);
        public IHttpConnectedServiceBuilder EnsureSecureConnection();

        public IHttpConnectedServiceBuilder WithServiceEndpointUrl(string endpointUrl);


    }
}
