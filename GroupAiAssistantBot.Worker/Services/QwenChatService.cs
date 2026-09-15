using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GroupAiAssistantBot.Worker.Services;

public record ChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages
);

public record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);
public record ChatResponse([property: JsonPropertyName("choices")] ChatChoice[] Choices);

public class QwenChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QwenSettings _qwenSettings;
    private readonly ChatContextManager _contextManager;
    private readonly ILogger<QwenChatService> _logger;

    public QwenChatService(
        IHttpClientFactory httpClientFactory,
        IOptions<QwenSettings> qwenSettings,
        ChatContextManager contextManager,
        ILogger<QwenChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _qwenSettings = qwenSettings.Value;
        _contextManager = contextManager;
        _logger = logger;
    }

    public async Task<string> GetResponseAsync(long chatId, string author, string userText, CancellationToken ct)
    {
        // 1. Добавляем сообщение пользователя в историю
        _contextManager.AddMessage(chatId, "user", $"{author}: {userText}");

        // 2. Берем безопасную копию истории для отправки
        List<ChatMessage> historySnapshot;
        lock (_contextManager.GetHistory(chatId))
        {
            historySnapshot = _contextManager.GetHistory(chatId).ToList();
        }

        // 3. Формируем запрос к API
        var request = new ChatRequest(_qwenSettings.Model, historySnapshot);

        var httpClient = _httpClientFactory.CreateClient("QwenClient");

        _logger.LogInformation("🧠 Отправка запроса к Qwen для чата {ChatId}...", chatId);

        var response = await httpClient.PostAsJsonAsync(
            "/chat/completions",
            request,
            new System.Text.Json.JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull },
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        string aiReply = result?.Choices?[0].Message.Content ?? "⚠️ ИИ вернул пустой ответ.";

        // 4. Сохраняем ответ ИИ в историю
        _contextManager.AddMessage(chatId, "assistant", aiReply);

        return aiReply;
    }
}