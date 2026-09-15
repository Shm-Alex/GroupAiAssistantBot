using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace GroupAiAssistantBot.Worker.Services;

public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

public class ChatContextManager
{
    private readonly int _maxHistory;
    private readonly ConcurrentDictionary<long, List<ChatMessage>> _contexts = new();
    private readonly string _systemPrompt = "Ты — GroupAiAssistant, умный, вежливый и полезный помощник в групповых чатах. Отвечай кратко, по делу и на русском языке. Учитывай контекст беседы.";

    public ChatContextManager(IOptions<BotSettings> settings)
    {
        _maxHistory = settings.Value.MaxHistoryMessages;
    }

    public List<ChatMessage> GetHistory(long chatId)
    {
        return _contexts.GetOrAdd(chatId, _ => new List<ChatMessage>
        {
            new ChatMessage("system", _systemPrompt)
        });
    }

    public void AddMessage(long chatId, string role, string content)
    {
        if (_contexts.TryGetValue(chatId, out var history))
        {
            lock (history)
            {
                history.Add(new ChatMessage(role, content));

                // Обрезаем историю, чтобы не превышать лимиты API и не платить лишнее
                // Оставляем 1 системное сообщение + N последних сообщений
                if (history.Count > _maxHistory + 1)
                {
                    var systemMsg = history[0];
                    history.RemoveRange(1, history.Count - _maxHistory - 1);
                }
            }
        }
    }

    public void ClearHistory(long chatId)
    {
        if (_contexts.TryGetValue(chatId, out var history))
        {
            lock (history)
            {
                history.Clear();
                history.Add(new ChatMessage("system", _systemPrompt));
            }
        }
    }
}

// Класс для настроек из appsettings
public class BotSettings { public int MaxHistoryMessages { get; set; } = 30; }
public class QwenSettings { public string ApiKey { get; set; } = ""; public string BaseUrl { get; set; } = ""; public string Model { get; set; } = ""; }