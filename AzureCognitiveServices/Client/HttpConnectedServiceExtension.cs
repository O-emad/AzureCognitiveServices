using Microsoft.Azure.CognitiveServices.Vision.Face;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision;

namespace AzureCognitiveServices.Client
{
    public static class HttpConnectedServiceExtension
    {
        public static VisionApiServiceConnection AsVisionApiService(this ServiceHttpConnection connection)
        {
            var visionClient = new VisionApiServiceConnection(connection);
            VisionAPI.ApiKeyServiceClientCredentials credentials = new VisionAPI.ApiKeyServiceClientCredentials(visionClient.ServiceProviderKey);
            visionClient.Client = new VisionAPI.ComputerVisionClient(credentials)
            {
                Endpoint = visionClient.ServiceProviderEndpoint
            };
            return visionClient;
        }

        public static FaceApiServiceConnection AsFaceApiService(this ServiceHttpConnection connection)
        {
            var faceClient = new FaceApiServiceConnection(connection);
            FaceAPI.ApiKeyServiceClientCredentials credentials = new FaceAPI.ApiKeyServiceClientCredentials(faceClient.ServiceProviderKey);
            faceClient.Client = new FaceAPI.FaceClient(credentials)
            {
                Endpoint = faceClient.ServiceProviderEndpoint
            };
            return faceClient;
        }


    }
}
