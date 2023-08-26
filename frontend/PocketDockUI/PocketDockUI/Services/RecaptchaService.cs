using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PocketDockUI.Models;

namespace PocketDockUI.Services;

public class RecaptchaService
{
    private readonly RecaptchaConfig _config;
    private readonly ILogger<RecaptchaService> _logger;

    public RecaptchaService(IOptions<RecaptchaConfig> config, ILogger<RecaptchaService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }
    public async Task<bool> Verify(string recaptchaToken)
    {
        var body = new Dictionary<string, string>
        {
            ["secret"] = _config.SecretKey,
            ["response"] = recaptchaToken
        };

        try
        {
            using var client = new HttpClient();
            var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", new FormUrlEncodedContent(body));
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<RecaptchaResponse>(json);
            return result?.Success == true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Recaptcha verification failed");
            return false;
        }
    }
    
    class RecaptchaResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}