using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face;

namespace AzureCognitiveServices.Client
{
    public class FaceApiServiceConnection : ServiceHttpConnection
    {
        public FaceAPI.FaceClient Client { get; set; }
        public FaceApiServiceConnection(ServiceHttpConnection connection)
            :base(connection)
        {

        }
        public FaceApiServiceConnection()
        {

        }
    }
}
