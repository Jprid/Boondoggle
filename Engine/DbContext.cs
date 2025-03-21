﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Engine.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Engine;

public class DbContext
{
    private readonly IConfiguration _configuration;
    private readonly string? _connectionString;
    private readonly ILogger<DbContext> _logger;

    public DbContext(IConfiguration config, ILogger<DbContext> logger)
    {
        _configuration = config;
        _logger = logger;
        _connectionString = config["SQLite:ConnectionString"];
        Initialize();
        ImportCsvToSqlite(@"Data\ufo.csv");
    }

    public void Initialize()
    {
        using var conn = new SQLiteConnection(_connectionString);
        conn.Open();
        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS ImageTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TypeName TEXT NOT NULL UNIQUE)",
            @"CREATE TABLE IF NOT EXISTS BlueBook (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    City VARCHAR(50),
                    ColorsReported VARCHAR(50),
                    ShapeReported VARCHAR(50),
                    State VARCHAR(2),
                    Time TEXT
                )",
            @"CREATE TABLE IF NOT EXISTS Images (
                    Id INTEGER NOT NULL,
                    FileName TEXT NOT NULL,
                    UploadDate TEXT NOT NULL,
                    Location TEXT,
                    CaptureDateTime TEXT,
                    ImageTypeId INTEGER NOT NULL,
                    ImageData BLOB NOT NULL,
                    PRIMARY KEY (Id, ImageTypeId),
                    FOREIGN KEY (ImageTypeId) REFERENCES ImageTypes(Id) ON DELETE RESTRICT)",
            @"CREATE TABLE IF NOT EXISTS Reports (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    ReportDate TEXT NOT NULL);",
            @"CREATE TABLE IF NOT EXISTS ReportImages (
                    ReportId INTEGER NOT NULL,
                    ImageId INTEGER NOT NULL,
                    ImageTypeId INTEGER NOT NULL,
                    PRIMARY KEY (ReportId, ImageId, ImageTypeId),
                    FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ImageId, ImageTypeId) REFERENCES Images(Id, ImageTypeId) ON DELETE CASCADE);",
            @"CREATE TABLE IF NOT EXISTS Comments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReportId INTEGER NOT NULL,
                    CommentText TEXT NOT NULL,
                    CommentDate TEXT NOT NULL,
                    FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE);"
        };

        foreach (var cmdText in commands)
        {
            using var cmd = new SQLiteCommand(cmdText, conn);
            cmd.ExecuteNonQuery();
        }

        var seedTypes = @"INSERT OR IGNORE INTO ImageTypes (TypeName) VALUES 
                ('Original'), ('ELA'), ('Annotated')";
        using (var cmd = new SQLiteCommand(seedTypes, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    void ImportCsvToSqlite(string csvPath)
    {
        using var connection = new SQLiteConnection(_connectionString);
        connection.Open();
        long count = 0;
        using (var verifyCmd = new SQLiteCommand("SELECT COUNT(*) FROM main.BlueBook", connection))
        {
            count = (long)verifyCmd.ExecuteScalar();
            Console.WriteLine($"Rows in BlueBook after commit: {count}");
        }

        if (count > 0)
        {
            _logger.LogInformation($"Bluebook sightings already imported. Skipping.");
            return;
        }


        // Begin transaction for better performance
        using var transaction = connection.BeginTransaction();
        try
        {
            string insertSql = @"
                        INSERT OR IGNORE INTO main.BlueBook (City, ColorsReported, ShapeReported, State, Time)
                        VALUES (@City, @ColorsReported, @ShapeReported, @State, @Time);";

            using var command = new SQLiteCommand(insertSql, connection);
            // Create parameters once
            command.Parameters.Add("@City", DbType.String);
            command.Parameters.Add("@ColorsReported", DbType.String);
            command.Parameters.Add("@ShapeReported", DbType.String);
            command.Parameters.Add("@State", DbType.String);
            command.Parameters.Add("@Time", DbType.String);
            // Read CSV file
            using var reader = new StreamReader(csvPath);
            // Skip header if it exists
            string headerLine = reader.ReadLine();

            // Process data rows
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] values = ParseCsvLine(line);

                // Skip if we don't have enough columns
                if (values.Length < 5)
                {
                    Console.WriteLine($"Skipping invalid row: {line}");
                    continue;
                }

                // Set parameter values
                command.Parameters["@City"].Value = values[0];
                command.Parameters["@ColorsReported"].Value = values[1];
                command.Parameters["@ShapeReported"].Value = values[2];
                command.Parameters["@State"].Value = values[3];
                command.Parameters["@Time"].Value = values[4];
                // Execute insert
                int affected = command.ExecuteNonQuery();
                if (affected != 1)
                {
                    Console.WriteLine($"Unexpected result for row: {line} - Rows affected: {affected}");
                }
            }

            // Commit the transaction
            transaction.Commit();
            Console.WriteLine($"Successfully imported csv to sqlite: {csvPath} - {connection.Changes}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during import: {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    // Simple CSV parser that handles quotes
    private string[] ParseCsvLine(string? line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var field = new StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(field.ToString().Trim());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        // Add the last field
        result.Add(field.ToString().Trim());

        return result.ToArray();
    }

    // public async Task<ImageViewModel> GetLatestImageAsync()
    //     {
    //         _logger.LogDebug("Fetching latest image from database");
    //         try
    //         {
    //             using (var conn = new SQLiteConnection(_connectionString))
    //             {
    //                 await conn.OpenAsync();
    //                 var cmd = new SQLiteCommand(
    //                     @"SELECT i.*
    //                           FROM Images i
    //                           INNER JOIN ImageTypes it ON i.ImageTypeId = it.Id AND it.TypeName = 'Original'
    //                           ORDER BY i.UploadDate DESC LIMIT 1", conn);
    //                 using (var reader = cmd.ExecuteReader())
    //                 {
    //                     if (reader.Read())
    //                     {
    //                         var uploadedImage = new UploadedImage
    //                         (
    //                             id: reader.GetInt64(0),
    //                             fileName: reader.GetString(1),
    //                             uploadDate: reader.GetString(2),
    //                             location: reader.IsDBNull(3) ? null : reader.GetString(3),
    //                             captureDateTime: reader.IsDBNull(4) ? null : reader.GetString(4),
    //                             imageData: (byte[])reader["ImageData"]
    //                         );
    //                         return _mapper.Map<ImageViewModel>(uploadedImage);
    //                     }
    //                 }
    //             }
    //
    //             return null;
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogError(ex, "Error retrieving latest image");
    //             throw;
    //         }
    //     }

    public async Task<long> InsertImageAsync(string filePath, string location, string captureDateTime,
        byte[] originalData, byte[] elaData)
    {
        _logger.LogDebug("Inserting new image with versions");

        try
        {
            await using var conn = new SQLiteConnection(_connectionString);
            await conn.OpenAsync();
            using (var transaction = conn.BeginTransaction())
            {
                // Insert metadata into Images
                var cmd = new SQLiteCommand(
                    "INSERT INTO Images (FileName, UploadDate, Location, CaptureDateTime) VALUES (@fileName, @uploadDate, @location, @captureDateTime)",
                    conn);
                cmd.Parameters.AddWithValue("@fileName", System.IO.Path.GetFileName(filePath));
                cmd.Parameters.AddWithValue("@uploadDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                cmd.Parameters.AddWithValue("@location", location);
                cmd.Parameters.AddWithValue("@captureDateTime", captureDateTime);
                cmd.ExecuteNonQuery();
                long imageId = conn.LastInsertRowId;

                // Insert versions into ImageVersions
                long originalTypeId = GetImageTypeId(conn, "Original");
                long elaTypeId = GetImageTypeId(conn, "ELA");

                cmd = new SQLiteCommand(
                    "INSERT INTO ImageVersions (ImageId, ImageTypeId, ImageData) VALUES (@imageId, @imageTypeId, @imageData)",
                    conn);
                cmd.Parameters.AddWithValue("@imageId", imageId);
                cmd.Parameters.AddWithValue("@imageTypeId", originalTypeId);
                cmd.Parameters.AddWithValue("@imageData", originalData);
                cmd.ExecuteNonQuery();

                cmd.Parameters.Clear();
                cmd.CommandText =
                    "INSERT INTO ImageVersions (ImageId, ImageTypeId, ImageData) VALUES (@imageId, @imageTypeId, @imageData)";
                cmd.Parameters.AddWithValue("@imageId", imageId);
                cmd.Parameters.AddWithValue("@imageTypeId", elaTypeId);
                cmd.Parameters.AddWithValue("@imageData", elaData);
                cmd.ExecuteNonQuery();

                transaction.Commit();
                return imageId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting image");
            throw;
        }
    }

    async Task<long> InsertReportAsync(string title, string description,
        (long Id, int ImageTypeId)[] imageRefs)
    {
        _logger.LogDebug("Inserting new report");
        try
        {
            await using var conn = new SQLiteConnection(_connectionString);
            await conn.OpenAsync();
            using (var transaction = conn.BeginTransaction())
            {
                // Insert report
                var cmd = new SQLiteCommand(
                    "INSERT INTO Reports (Title, Description, ReportDate) VALUES (@title, @description, @reportDate)",
                    conn);
                cmd.Parameters.AddWithValue("@title", title);
                cmd.Parameters.AddWithValue("@description", description);
                cmd.Parameters.AddWithValue("@reportDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                await cmd.ExecuteNonQueryAsync();
                long reportId = conn.LastInsertRowId;

                // Link images
                foreach (var (imageId, imageTypeId) in imageRefs)
                {
                    cmd = new SQLiteCommand(
                        "INSERT INTO ReportImages (ReportId, ImageId, ImageTypeId) VALUES (@reportId, @imageId, @imageTypeId)",
                        conn);
                    cmd.Parameters.AddWithValue("@reportId", reportId);
                    cmd.Parameters.AddWithValue("@imageId", imageId);
                    cmd.Parameters.AddWithValue("@imageTypeId", imageTypeId);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                _logger.LogInformation("Report inserted with ID: {ReportId}", reportId);
                return reportId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting report");
            throw;
        }
    }

    public async Task<long> InsertCommentAsync(long reportId, string commentText)
    {
        _logger.LogDebug("Inserting new comment for report ID {ReportId}", reportId);
        return await Task.Run(() =>
        {
            try
            {
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SQLiteCommand(
                        "INSERT INTO Comments (ReportId, CommentText, CommentDate) VALUES (@reportId, @commentText, @commentDate)",
                        conn);
                    cmd.Parameters.AddWithValue("@reportId", reportId);
                    cmd.Parameters.AddWithValue("@commentText", commentText);
                    cmd.Parameters.AddWithValue("@commentDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                    cmd.ExecuteNonQuery();
                    long commentId = conn.LastInsertRowId;
                    conn.Close();
                    _logger.LogInformation("Comment inserted with ID: {CommentId}", commentId);
                    return commentId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting comment");
                throw;
            }
        });
    }

    private long GetImageTypeId(SQLiteConnection conn, string typeName)
    {
        var cmd = new SQLiteCommand("SELECT Id FROM ImageTypes WHERE TypeName = @typeName", conn);
        cmd.Parameters.AddWithValue("@typeName", typeName);
        var result = cmd.ExecuteScalar();
        if (result == null) throw new Exception($"Image type '{typeName}' not found");
        return Convert.ToInt64(result);
    }
}