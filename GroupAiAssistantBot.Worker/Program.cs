using GroupAiAssistantBot.Worker;
using GroupAiAssistantBot.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    // 1. Настройки
    var botToken = hostContext.Configuration["Telegram:BotToken"]
                   ?? throw new InvalidOperationException("❌ Telegram Bot Token не найден!");

    services.Configure<QwenSettings>(hostContext.Configuration.GetSection("Qwen"));
    services.Configure<BotSettings>(hostContext.Configuration.GetSection("BotSettings"));

    // 2. Клиенты
    services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

    services.AddHttpClient("QwenClient", (sp, client) =>
    {
        var qwenOptions = sp.GetRequiredService<IOptions<QwenSettings>>().Value;
        client.BaseAddress = new Uri(qwenOptions.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", qwenOptions.ApiKey);
        client.Timeout = TimeSpan.FromSeconds(120); // ИИ может думать долго
    });

    // 3. Наши сервисы
    services.AddSingleton<ChatContextManager>();
    services.AddSingleton<QwenChatService>();
    services.AddHostedService<TelegramBotWorker>();
});

await builder.Build().RunAsync();