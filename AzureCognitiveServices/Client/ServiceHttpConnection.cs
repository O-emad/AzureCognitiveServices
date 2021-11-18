using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureCognitiveServices.Client
{
    public class ServiceHttpConnection
    {
        public string ServiceProviderKey { get; set; }
        public string ServiceProviderEndpoint { get; set; }
        public bool UseHttps { get; set; }

        public ServiceHttpConnection()
        {

        }
        public ServiceHttpConnection(ServiceHttpConnection connection)
        {
            ServiceProviderEndpoint = connection.ServiceProviderEndpoint;
            ServiceProviderKey = connection.ServiceProviderKey;
            UseHttps = connection.UseHttps;
        }
    }
}
