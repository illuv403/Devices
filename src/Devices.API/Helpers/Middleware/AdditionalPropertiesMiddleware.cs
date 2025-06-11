using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Reflection;
using Devices.API;

namespace Devices.API.Helpers.Middleware;

public class AdditionalPropertiesMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdditionalPropertiesMiddleware> _logger;
    private readonly string _path = "example_validation_rules.json";

    public AdditionalPropertiesMiddleware(RequestDelegate next, ILogger<AdditionalPropertiesMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        async Task<JsonNode?> LoadValidationRules()
        {
            if (!File.Exists(_path))
            {
                _logger.LogInformation($"File not found at {_path}, middleware will be skipped.");
                return null;
            }

            var rulesContent = await File.ReadAllTextAsync(_path);
            _logger.LogInformation($"Validation rules loaded from {_path}");
            return JsonNode.Parse(rulesContent);
        }

        async Task<bool> ValidateRequest(HttpContext context, Device device, JsonNode requestJson, JsonNode validationRules, string deviceType)
        {
            var validations = validationRules["validations"]?.AsArray();
            if (validations == null) return true;

            foreach (var validation in validations)
            {
                if (validation?["type"]?.ToString().Equals(deviceType, StringComparison.OrdinalIgnoreCase) != true)
                    continue;

                var preRequestName = validation["preRequestName"]?.ToString();
                var preRequestValue = validation["preRequestValue"]?.ToString();
                if (!string.IsNullOrEmpty(preRequestName) && !string.IsNullOrEmpty(preRequestValue))
                {
                    var property = typeof(Device).GetProperty(preRequestName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property == null) return true;

                    var actualPreValue = property.GetValue(device)?.ToString();
                    if (actualPreValue == null || !actualPreValue.Equals(preRequestValue, StringComparison.OrdinalIgnoreCase)) return true;
                }

                if (string.IsNullOrEmpty(device.AdditionalProperties)) return true;

                Dictionary<string, string> additionalProps;
                try
                {
                    additionalProps = JsonSerializer.Deserialize<Dictionary<string, string>>(device.AdditionalProperties);
                }
                catch
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Invalid additionalProperties JSON format" }));
                    return false;
                }

                if (additionalProps == null) return true;

                var rules = validation["rules"]?.AsArray();
                if (rules == null) return true;

                foreach (var rule in rules)
                {
                    var paramName = rule?["paramName"]?.ToString();
                    if (string.IsNullOrEmpty(paramName)) continue;

                    if (!additionalProps.TryGetValue(paramName, out var paramValue) || paramValue == null) continue;

                    var regexField = rule["regex"];
                    if (regexField == null) continue;

                    if (regexField.GetValueKind() == JsonValueKind.String)
                    {
                        string regexPattern = regexField.ToString();
                        if (!Regex.IsMatch(paramValue, regexPattern))
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = $"Parameter '{paramName}' does not match required pattern" }));
                            return false;
                        }
                    }
                    else if (regexField.GetValueKind() == JsonValueKind.Array)
                    {
                        var allowedValues = regexField.AsArray();
                        bool valueFound = allowedValues.Any(v => v?.ToString().Equals(paramValue, StringComparison.OrdinalIgnoreCase) == true);
                        if (!valueFound)
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = $"Parameter '{paramName}' has invalid value. Allowed values: {string.Join(", ", allowedValues.Select(v => v?.ToString()))}" }));
                            return false;
                        }
                    }
                }

                break;
            }

            return true;
        }

        _logger.LogInformation("Middleware called");

        try
        {
            if (!(context.Request.Method == "POST" || context.Request.Method == "PUT") ||
                context.Request.ContentType?.ToLower().Contains("application/json") != true)
            {
                await _next(context);
                _logger.LogInformation("Middleware completed");
                return;
            }

            var validationRules = await LoadValidationRules();
            if (validationRules == null)
            {
                await _next(context);
                _logger.LogInformation("Middleware completed");
                return;
            }

            context.Request.EnableBuffering();
            
            string rawJson;
            using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
            {
                rawJson = await reader.ReadToEndAsync();
            }
            
            context.Request.Body.Position = 0;

            if (string.IsNullOrEmpty(rawJson))
            {
                await _next(context);
                _logger.LogInformation("Middleware completed");
                return;
            }

            var requestJson = JsonNode.Parse(rawJson);
            if (requestJson?["type"]?.ToString() is not string deviceType)
            {
                await _next(context);
                _logger.LogInformation("Middleware completed");
                return;
            }

            if (requestJson["additionalProperties"] is not JsonNode additionalNode ||
                additionalNode.ToJsonString() is not string additionalJson)
            {
                await _next(context);
                _logger.LogInformation("Middleware completed");
                return;
            }

            var device = new Device
            {
                Name = requestJson["name"]?.ToString() ?? "",
                IsEnabled = requestJson["isEnabled"]?.GetValue<bool>() ?? false,
                AdditionalProperties = additionalJson
            };

            _logger.LogInformation($"Validating device type: {deviceType}");
            if (!await ValidateRequest(context, device, requestJson, validationRules, deviceType))
            {
                return;
            }

            await _next(context);
            _logger.LogInformation($"Middleware completed successfully for device type: {deviceType}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Middleware error: {ex.Message}");
            context.Response.StatusCode = 500;
        }
    }
}