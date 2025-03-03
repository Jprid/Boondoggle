using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Engine.Services;

public record BrightnessAnalysisResult(Mat? Image = null, int SuspiciousBlockCount = 0);

public interface IBrightnessAnalyzerService
{
    BrightnessAnalysisResult AnalyzeBrightness(string imagePath, double width, double height, int blockSize);
}

public class BrightnessAnalyzerService : IBrightnessAnalyzerService
{
    private readonly ILogger<BrightnessAnalyzerService> _logger;

    public BrightnessAnalyzerService(ILogger<BrightnessAnalyzerService> logger)
    {
        _logger = logger;
    }

    public BrightnessAnalysisResult AnalyzeBrightness(string imagePath, double width, double height, int blockSize)
    {
        Mat img = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (img.Empty())
        {
            Console.WriteLine("Failed to load image.");
            return null;
        }

        // Create a grayscale version for histogram calculations
        Mat gray = new Mat();
        Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);

        // Define block size and calculate number of blocks to cover the entire image
        int nx = (int)Math.Ceiling((double)img.Width / blockSize); // Ensure all width is covered
        int ny = (int)Math.Ceiling((double)img.Height / blockSize); // Ensure all height is covered
        int suspiciousBlockCount = 0;
        // Compute histograms for each block
        Mat[,] histograms = new Mat[ny, nx];
        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                // Adjust block width/height for edge blocks
                int bw = (j == nx - 1) ? img.Width - j * blockSize : blockSize;
                int bh = (i == ny - 1) ? img.Height - i * blockSize : blockSize;
                Rect blockRect = new Rect(j * blockSize, i * blockSize, bw, bh);
                Mat block = new Mat(gray, blockRect);
                histograms[i, j] = new Mat();
                float[] range = { 0, 256 };
                int[] histSize = { 256 };
                Cv2.CalcHist(new Mat[] { block }, new int[] { 0 }, null, histograms[i, j], 1, histSize,
                    new float[][] { range });
                Cv2.Normalize(histograms[i, j], histograms[i, j], 0, 1, NormTypes.MinMax);
            }
        }

        // Clone the color image for drawing results
        Mat result = img.Clone();
        double threshold = 0.2;
        // Threshold for detecting suspicious blocks

        // Analyze blocks and draw outlines
        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                // Collect neighboring histograms
                var neighborHists = new List<Mat>();
                for (int di = -1; di <= 1; di++)
                {
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = i + di;
                        int nj = j + dj;
                        if (ni >= 0 && ni < ny && nj >= 0 && nj < nx)
                        {
                            neighborHists.Add(histograms[ni, nj]);
                        }
                    }
                }

                if (neighborHists.Count == 0) continue;

                // Compute average histogram of neighbors
                Mat avgHist = new Mat(histograms[i, j].Rows, histograms[i, j].Cols, histograms[i, j].Type());
                avgHist.SetTo(0);
                foreach (var hist in neighborHists)
                {
                    avgHist += hist;
                }

                avgHist /= neighborHists.Count;

                // Compare current block histogram with average neighbor histogram
                double distance = Cv2.CompareHist(histograms[i, j], avgHist, HistCompMethods.Chisqr);

                // Outline suspicious blocks
                if (distance > threshold)
                {
                    suspiciousBlockCount++;
                    int bw = (j == nx - 1) ? img.Width - j * blockSize : blockSize;
                    int bh = (i == ny - 1) ? img.Height - i * blockSize : blockSize;
                    Rect blockRect = new Rect(j * blockSize, i * blockSize, bw, bh);
                    Cv2.Rectangle(result, blockRect, Scalar.Red, 2); // Red outline on color image
                }
            }
        }
        // Save and display the full result
        return result;
    }
}