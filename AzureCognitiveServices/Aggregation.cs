using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureCognitiveServices
{
    static internal class Aggregation
    {
        public static KeyValuePair<string, double> GetDominantEmotion(Emotion scores)
        {
            return new Dictionary<string, double>()
                {
                    { "Anger", scores.Anger },
                    { "Contempt", scores.Contempt },
                    { "Disgust", scores.Disgust },
                    { "Fear", scores.Fear },
                    { "Happiness", scores.Happiness },
                    { "Neutral", scores.Neutral },
                    { "Sadness", scores.Sadness },
                    { "Surprise", scores.Surprise }
                }
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .First();
        }

        public static string SummarizeEmotion(Emotion scores)
        {
            var bestEmotion = Aggregation.GetDominantEmotion(scores);
            return string.Format("{0}: {1:N1}", bestEmotion.Key, bestEmotion.Value);
        }

        public static string SummarizeFaceAttributes(FaceAttributes attr)
        {
            List<string> attrs = new();
            if (attr.Gender.HasValue) attrs.Add(attr.Gender.Value.ToString());
            if (attr.Age > 0) attrs.Add(attr.Age.ToString());
            if (attr.HeadPose != null)
            {
                // Simple rule to estimate whether person is facing camera. 
                bool facing = Math.Abs(attr.HeadPose.Yaw) < 25;
                attrs.Add(facing ? "facing camera" : "not facing camera");
            }
            return string.Join(", ", attrs);
        }
    }
}
