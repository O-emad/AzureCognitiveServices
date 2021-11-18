
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace AzureCognitiveServices
{
    public class LiveCameraResult
    {
        public FaceAPI.DetectedFace[] Faces { get; set; } = null;
        public string[] CelebrityNames { get; set; } = null;
        public VisionAPI.ImageTag[] Tags { get; set; } = null;
    }
}