using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;

namespace Engine.Services;

public interface IAzureMapsService
{
    void GenerateMapsAsync(WebView2 webView);
    void UpdateMapAsync(WebView2 webView, double longitude, double latitude, string markerTitle, int zoom);
}

public class AzureMapsService : IAzureMapsService
{
    private readonly ILogger<IAzureMapsService> _logger;
    private readonly IConfiguration _configuration;
    public string? _azureMapsKey { get; private set; } = "";
    public string Title = "";

    public AzureMapsService(ILogger<IAzureMapsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _azureMapsKey = configuration["AzureMaps:Key"];
    }

    public async void GenerateMapsAsync(WebView2 webView)
    {
        string htmlContent = $@"
                        <html>
                        <head>
                            <title>Azure Maps</title>
                            <script type='text/javascript' src='https://atlas.microsoft.com/sdk/javascript/mapcontrol/2/atlas.min.js'></script>
                            <style>
                                #myMap {{ position: relative; width: 100%; height: 100%; }}
                            </style>
                        </head>
                        <body>
                            <div id='myMap'></div>
                            <script>
                                var map;
                                function initializeMap() {{
                                    map = new atlas.Map('myMap', {{
                                        center: [-122.33, 47.61], // Default: Seattle
                                        zoom: 12,
                                        authOptions: {{
                                            authType: 'subscriptionKey',
                                            subscriptionKey: '{_azureMapsKey}' // Replace with your key
                                        }}
                                    }});
                                    map.events.add('ready', function () {{
                                        console.log('Map is ready');
                                    }});
                                }}
                
                                // Function to update map center and zoom
                                function updateMapCenter(longitude, latitude, zoom) {{
                                    if (map) {{
                                        map.setCamera({{
                                            center: [longitude, latitude],
                                            zoom: zoom || 12
                                        }});
                                    }}
                                }}
                
                                // Function to add a marker
                                function addMarker(longitude, latitude, title) {{
                                    if (map) {{
                                        var marker = new atlas.HtmlMarker({{
                                            htmlContent: '<div>' + title + '</div>',
                                            position: [longitude, latitude]
                                        }});
                                        map.markers.add(marker);
                                    }}
                                }}
                
                                // Call initializeMap when the page loads
                                window.onload = initializeMap;
                            </script>
                        </body>
                        </html>";
        await webView.EnsureCoreWebView2Async(null);
        webView.NavigateToString(htmlContent);
    }

    public async void UpdateMapAsync(WebView2 webView, double longitude, double latitude, string markerTitle, int zoom = 12)
    {
        await webView.ExecuteScriptAsync($"updateMapCenter({longitude}, {latitude}, {zoom}");
        await webView.ExecuteScriptAsync($"addMarker({longitude}, {latitude}, '{markerTitle}')");
    }
}