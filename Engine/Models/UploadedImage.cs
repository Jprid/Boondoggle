namespace Engine.Models;

public record UploadedImage(
    long id,
    string uploadDate,
    byte[] imageData,
    string fileName,
    string location,
    string captureDateTime
)
{
    int Fn() => 0;
};
