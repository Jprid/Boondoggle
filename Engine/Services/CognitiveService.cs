using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine.Models;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;

namespace Engine.Services;

public interface ICognitiveService
{
    Task<List<AnalysisResult>> AnalyzeImageAsync(string imageData);
}

public class CognitiveService : ICognitiveService
{
    private readonly ComputerVisionClient _client;

    public CognitiveService(IConfiguration config)
    {
        _client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(config["CognitiveServices:Key"]))
        {
            Endpoint = config["CognitiveServices:Endpoint"]
        };
    }

    public async Task<List<AnalysisResult>> AnalyzeImageAsync(string imagePath)
    {
        var results = new List<AnalysisResult>();
        await using var stream = File.OpenRead(imagePath);
        var features = new List<VisualFeatureTypes?>
        {
            VisualFeatureTypes.Objects,
            VisualFeatureTypes.Description,
            VisualFeatureTypes.Tags,
            VisualFeatureTypes.Categories,
            VisualFeatureTypes.Color,
        };

        var analysis = await _client.AnalyzeImageInStreamAsync(stream, features);
        // Objects
        if (analysis.Objects?.Count > 0)
        {
            results.AddRange(analysis.Objects.Select(obj => new AnalysisResult
            {
                FeatureType = "Object",
                Name = obj.ObjectProperty,
                Confidence = obj.Confidence
            }));
        }

        results.Add(new AnalysisResult());
        // Description
        if (analysis.Description?.Captions?.Count > 0)
        {
            results.AddRange(
                analysis.Description.Captions.Select(c => new AnalysisResult
                {
                    FeatureType = "Description",
                    Name = c.Text,
                    Confidence = c.Confidence
                }));
        }

        results.Add(new AnalysisResult());
        // Tags
        if (analysis.Tags?.Count > 0)
        {
            results.AddRange(analysis.Tags.Select(tag => new AnalysisResult
            {
                FeatureType = "Tag",
                Name = tag.Name,
                Confidence = tag.Confidence
            }));
        }

        // Categories
        if (analysis.Categories?.Count > 0)
        {
            results.AddRange(analysis.Categories.Select(cat => new AnalysisResult
            {
                FeatureType = "Category",
                Name = cat.Name,
                Confidence = cat.Score
            }));
        }

        // Color
        if (analysis.Color != null)
        {
            results.Add(new AnalysisResult
            {
                FeatureType = "Color",
                Name = $"Dominant Foreground: {analysis.Color.DominantColorForeground}",
                Confidence = null
            });
            results.Add(new AnalysisResult
            {
                FeatureType = "Color",
                Name = $"Dominant Background: {analysis.Color.DominantColorBackground}",
                Confidence = null
            });
            if (!string.IsNullOrEmpty(analysis.Color.AccentColor))
            {
                results.Add(new AnalysisResult
                {
                    FeatureType = "Color",
                    Name = $"Accent Color: #{analysis.Color.AccentColor}",
                    Confidence = null
                });
            }
        }

        return results;
    }
}