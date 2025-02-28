namespace Engine.Models;

public class AnalysisResult
{
    public string FeatureType { get; set; }
    public string Name { get; set; }
    public double? Confidence { get; set; }
}