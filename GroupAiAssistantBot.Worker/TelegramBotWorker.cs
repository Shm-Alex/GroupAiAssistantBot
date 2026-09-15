using GroupAiAssistantBot.Worker.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GroupAiAssistantBot.Worker;

public class TelegramBotWorker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly QwenChatService _qwenService;
    private readonly ChatContextManager _contextManager;
    private readonly ILogger<TelegramBotWorker> _logger;

    public TelegramBotWorker(
        ITelegramBotClient botClient,
        QwenChatService qwenService,
        ChatContextManager contextManager,
        ILogger<TelegramBotWorker> logger)
    {
        _botClient = botClient;
        _qwenService = qwenService;
        _contextManager = contextManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🤖 Бот запускается и начинает слушать сообщения...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>(),
            DropPendingUpdates = true
        };

        var me = await _botClient.GetMe(stoppingToken);
        _logger.LogInformation($"✅ Бот успешно авторизован как: @{me.Username}");

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message is not { } message) return;
        if (string.IsNullOrEmpty(message.Text)) return;

        long chatId = message.Chat.Id;
        string author = message.From?.FirstName ?? "Неизвестный";
        string text = message.Text.Trim();

        _logger.LogInformation($"💬 Сообщение от {author} в чате {chatId}: {text}");

        // Обработка команды очистки контекста
        if (text.Equals("/clear", StringComparison.OrdinalIgnoreCase))
        {
            _contextManager.ClearHistory(chatId);
            await botClient.SendMessage(chatId, "🧹 Контекст чата очищен. Начинаем с чистого листа!", cancellationToken: cancellationToken);
            return;
        }

        try
        {
            // Получаем ответ от ИИ (он сам добавит сообщение в историю)
            string aiReply = await _qwenService.GetResponseAsync(chatId, author, text, cancellationToken);

            // Отправляем ответ в чат (с ответом на конкретное сообщение через ReplyParameters)
            await botClient.SendMessage(
                chatId: chatId,
                text: aiReply,
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);

            _logger.LogInformation($"✅ Ответ отправлен в чат {chatId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при получении ответа от ИИ");
            await botClient.SendMessage(chatId, "⚠️ Произошла ошибка при обращении к мозговому центру. Попробуйте позже.", cancellationToken: cancellationToken);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error: [{apiEx.ErrorCode}] {apiEx.Message}",
            _ => exception.ToString()
        };
        _logger.LogWarning(errorMessage);
        return Task.CompletedTask;
    }
}