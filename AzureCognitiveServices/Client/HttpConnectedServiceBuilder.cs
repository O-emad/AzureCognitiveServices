using Microsoft.Azure.CognitiveServices.Vision.Face;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AzureCognitiveServices.Client
{
    public class HttpConnectedServiceBuilder : IHttpConnectedServiceBuilder
    {
        private readonly ServiceHttpConnection connection;

        public HttpConnectedServiceBuilder()
        {
            connection = new ServiceHttpConnection();
        }

        public IHttpConnectedServiceBuilder EnsureSecureConnection()
        {
            connection.UseHttps = true;
            return this;
        }

        public IHttpConnectedServiceBuilder WithServiceEndpointUrl(string endpointUrl)
        {
            Uri resultUri;
            var result = Uri.TryCreate(endpointUrl, UriKind.RelativeOrAbsolute, out resultUri);
            if (connection.UseHttps) result = result && (resultUri.Scheme == Uri.UriSchemeHttps);
            //case of a valid url and no TLS is required
            if (result)
            {
                connection.ServiceProviderEndpoint = endpointUrl;
                return this;
            }
            else
            {
                throw new UriFormatException("The entered Url is not a valid url, please make sure to use TLS certified url in case of use \'EnsureSecureConnection\'");
            }
        }

        public IHttpConnectedServiceBuilder WithServiceKey(string key)
        {
            connection.ServiceProviderKey = key;
            return this;
        }

        internal ServiceHttpConnection Build() => connection;
    }
}
