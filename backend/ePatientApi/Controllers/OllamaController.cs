using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OllamaController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OllamaController> _logger;

        public OllamaController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OllamaController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("OllamaClient");
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// POST /api/ollama/chat - streaming chat completions
        /// </summary>
        [HttpPost("chat")]
        public async Task Chat([FromBody] OllamaChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserMessage))
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(new { error = "UserMessage is required" });
                return;
            }

            var apiBase = _configuration["Ollama:ApiBase"];
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                _logger.LogError("Ollama:ApiBase configuration is missing.");
                Response.StatusCode = 500;
                await Response.WriteAsJsonAsync(new { error = "Ollama configuration is missing" });
                return;
            }

            try
            {
                var payload = new
                {
                    model = "qwen2.5:0.5b",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a medical documentation assistant. Write only about the medical condition described. Never invent or add patient demographics like age, gender, or personal details. Follow instructions exactly." },
                        new { role = "user", content = request.UserMessage }
                    },
                    stream = true,
                    max_tokens = 500
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Response.ContentType = "text/event-stream";
                Response.Headers.Append("Cache-Control", "no-cache");
                Response.Headers.Append("Connection", "keep-alive");

                var requestMsg = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/v1/chat/completions") { Content = content };
                using var response = await _httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Ollama API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                    await Response.WriteAsync($"data: {{\"error\": \"Ollama API error\"}}\n\n");
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (line.StartsWith("data: "))
                        {
                            await Response.WriteAsync(line + "\n\n");
                            await Response.Body.FlushAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming chat");
                await Response.WriteAsync($"data: {{\"error\": \"{ex.Message}\"}}\n\n");
            }
        }

        /// <summary>
        /// GET /api/ollama/models - list available models
        /// </summary>
        [HttpGet("models")]
        public async Task<IActionResult> ListModels()
        {
            var apiBase = _configuration["Ollama:ApiBase"];
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                _logger.LogError("Ollama:ApiBase configuration is missing.");
                return StatusCode(500, new { error = "Ollama configuration is missing" });
            }

            try
            {
                var response = await _httpClient.GetAsync($"{apiBase}/v1/models");
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
                    return StatusCode((int)response.StatusCode, new { error = "Ollama API error", details = responseBody });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                if (result.TryGetProperty("data", out var dataProp))
                {
                    return Ok(dataProp);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Ollama list models");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class OllamaChatRequest
    {
        public string UserMessage { get; set; } = "";
    }
}
