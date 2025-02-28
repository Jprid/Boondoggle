using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using AutoMapper;
using Engine.Services;
using MetadataExtractor;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Maps.MapControl.WPF;
using OpenCvSharp;
using OpenCvSharp.Extensions;
// Required for BitmapConverter
using Window = System.Windows.Window;

namespace Engine;

public partial class MainWindow : Window
{
    private readonly IMapper _mapper;
    private readonly ICognitiveService _cognitiveServices;
    private readonly IAzureMapsService _azureMapService;
    private string currentImagePath;
    private Mat originalImage;
    private Mat elaImage;
    private double latitude;
    private double longitude;

    public MainWindow(IMapper mapper, ICognitiveService cognitiveServices, IAzureMapsService azureMapService)
    {
        InitializeComponent();
        ViewModeSelector.SelectedIndex = 0;
        currentImagePath = string.Empty;
        _mapper = mapper;
        _cognitiveServices = cognitiveServices;
        _azureMapService = azureMapService;
        InitializeMap();
    }


    // Handle image upload
    private void UploadImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image Files (*.jpg;*.png;*.webp)|*.jpg;*.png;*.webp;"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                currentImagePath = dialog.FileName;
                originalImage = new Mat(currentImagePath);
                MainImage.Source = BitmapSourceConverter.ToBitmapSource(originalImage);
                AnalyzeImage();
                UpdateMap();
                LoadComments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}");
            }
        }
    }

    private long InsertImage(string filePath, string location, string captureDateTime)
    {
        try
        {
            // return _dbContext.InsertImage(filePath, location, captureDateTime);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inserting image: {ex.Message}");
            return -1L;
        }

        return 0L;
    }

    // Analyze image (EXIF, ELA, Recognition)
    private async void AnalyzeImage()
    {
        if (string.IsNullOrEmpty(currentImagePath)) return;

        ExifDataText.Text = DisplayExifData();
        PerformELA();
        await PerformImageRecognition();
    }

    private async void InitializeMap()
    {
        // _azureMapService.GenerateMapsAsync(MapView);
    }


    // Display EXIF data
    private string DisplayExifData()
    {
        var sb = new StringBuilder("EXIF Data:");
        try
        {
            var tags = ImageMetadataReader.ReadMetadata(currentImagePath)
                .SelectMany(x => x.Tags);
            // pull out any location metadata
            var latlongmetadata = tags.Where(x => x.Name == "latitude" || x.Name == "longitude");
            if (latlongmetadata.Count() > 0)
            {
                latitude = Convert.ToDouble(tags.Single(x => x.Name == "latitude").Description);
                longitude = Convert.ToDouble(tags.Single(x => x.Name == "longitude").Description);
            }
            tags.Aggregate(sb, (acc, t) => acc.AppendLine($"{t.Name}: {t.Description}"));

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error reading EXIF data: {ex.Message}";
        }
    }

    // Perform Error Level Analysis (ELA)
    private void PerformELA()
    {
        try
        {
            using (var tempMat = new Mat())
            {
                // Save at lower quality
                var tempPath = Path.GetTempFileName() + ".jpg";
                Cv2.ImWrite(tempPath, originalImage, new int[] { (int)ImwriteFlags.JpegQuality, 95 });

                using (var recompressed = new Mat(tempPath))
                {
                    elaImage = new Mat();
                    Cv2.Absdiff(originalImage, recompressed, elaImage);
                    Cv2.ConvertScaleAbs(elaImage, elaImage, 10); // Amplify differences
                }

                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ELA Error: {ex.Message}");
        }
    }

    // Simulate image recognition
    // Perform image recognition using Microsoft Cognitive Services
    private async Task PerformImageRecognition()
    {
        var result = MessageBox.Show(
            "Would you like to analyze this image with Azure Cognitive Services?",
            "Analyze Image",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var analysisResults = await _cognitiveServices.AnalyzeImageAsync(currentImagePath);
            RecognitionResultsTable.ItemsSource = analysisResults;
        }
    }


    // Helper class for OpenCvSharp to WPF conversion
    public static class BitmapSourceConverter
    {
        public static BitmapSource ToBitmapSource(Mat mat)
        {
            using (var bitmap = mat.ToBitmap())
            {
                var hBitmap = bitmap.GetHbitmap();
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap);
                return source;
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }


    // Update map based on location input
    private async void UpdateMap()
    {
        try
        {
            string location = LocationInput.Text; // e.g., "Seattle, WA"
            string dateTime = DateTimeInput.Text; // e.g., "2023-10-01 14:30"
            int zoom = 12;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Error updating map");
            MessageBox.Show($"Error updating map: {ex.Message}");
        }
    }

    // Handle view mode selection
    private void ViewModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (originalImage == null) return;

        switch (ViewModeSelector.SelectedIndex)
        {
            case 0: // Original
                MainImage.Source = BitmapSourceConverter.ToBitmapSource(originalImage);
                break;
            case 1: // ELA
                if (elaImage != null)
                    MainImage.Source = BitmapSourceConverter.ToBitmapSource(elaImage);
                break;
            case 2: // Annotated (Placeholder)
                MainImage.Source = BitmapSourceConverter.ToBitmapSource(originalImage); // Placeholder
                break;
        }
    }


    // Submit user report
    private void SubmitReport_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder for report submission logic
        try
        {
            // var reportText = ReportDescription.Text;
            // var commentsList = _mapper.Map<List<ReportComment>>(reportId, reportText, CommentsList.Items);
            // _dbContext.CreateReport(reportText, originalImage);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error submitting report: {ex.Message}");
        }

        MessageBox.Show("Report submitted successfully!");
    }

    // Load and display comments
    private void LoadComments()
    {
        CommentsList.Items.Clear();
        // Placeholder for loading comments
    }

    // Post a new comment
    public void PostComment_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NewComment.Text))
        {
            CommentsList.Items.Add(NewComment.Text);
            NewComment.Text = "";
        }
    }
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // CreateMap<(long id, string text, long reportId), MainWindow.ReportComment>()
        //     .ForMember(dest => dest.id, opt => opt.MapFrom(src => src.id))
        //     .ForMember(dest => dest.text, opt => opt.MapFrom(src => src.text))
        //     .ForMember(dest => dest.reportId, opt => opt.MapFrom(src => src.reportId));
        //
        //
        // CreateMap<(long reportId, string reportDescription, List<MainWindow.ReportComment> comments),
        //         ReportModel>()
        //     .ForMember(dest => dest.id, opt => opt.MapFrom(src => src.reportId))
        //     .ForMember(dest => dest.comments, opt => opt.MapFrom((src) => src.comments))
        //     .ForMember(dest => dest.description, opt => opt.MapFrom(src => src.reportDescription));
    }
}