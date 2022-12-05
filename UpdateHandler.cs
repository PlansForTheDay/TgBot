using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using botTelegram.UpdateTypeHandlers;
using Update = Telegram.Bot.Types.Update;

namespace botTelegram.UpdateHandler;

public class UpdateHendler
{

    public static string PathToPhoto(long id)
    {
        string[] photo = Directory.GetFiles("user_photo", "*.png");
        string path = $"user_photo\\default.png";

        for (int i = 0; i < photo.Length; i++)
        {
            if (photo[i].Contains($"{id}"))
                path = photo[i];
        }

        return path;
    }
    

    public async static Task UpdateHandlerAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
    {
        var message = new BotOnMessageReceived();
        var notProcessed = new BotOnNotProcessedReceived();

        var handler = update.Type switch
        {
            UpdateType.Message => message.Handler(update.Message, botClient),
            UpdateType.CallbackQuery => BotOnCallbackQueryReceived.Handler(update.CallbackQuery, botClient),

            _ => notProcessed.Handler(update)
        };

        await handler;
    }
}