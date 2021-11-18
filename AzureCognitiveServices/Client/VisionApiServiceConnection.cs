using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision;

namespace AzureCognitiveServices.Client
{
    public class VisionApiServiceConnection : ServiceHttpConnection
    {
        public VisionAPI.ComputerVisionClient Client { get; set; }

        public VisionApiServiceConnection(ServiceHttpConnection connection)
            :base(connection)
        {

        }

        public VisionApiServiceConnection()
        {

        }
    }
}
