using botTelegram.DateBase;
using botTelegram.Models;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;
using Telegram.Bot.Types;
using botTelegram.ExtensionMethods;
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
    public async static void SendListEvent(Presence presence, Message message, ITelegramBotClient botClient)
    {
        using (BeerDbContext db = new BeerDbContext())
        {
            var @event = db.Events.First(e => e.Id == presence.IdEvent);
            var user = db.Users.First(u => u.Id == presence.IdUser);

            InlineKeyboardMarkup button = new(new[]
            {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Гости", $"guestsEvents:{user.Id}:{@event.Id}")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Изменить статус", $"changeEventRank:{user.Id}:{@event.Id}")
                    }
            });

            await botClient.SendTextMessageAsync(message.Chat.Id,
                //$"id: {ev.Id}\n" +
                $"Название: {@event.Title}\n" +
                $"Дата: {@event.Start.ToShortDateString()}\n" +
                $"Код присоединения: {@event.Code}\n" +
                $"Статус на мероприятии: {presence.Rank.ToLocaleString()}", replyMarkup: button);
        }
    }


    public async static Task UpdateHandlerAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
    {
        var message = new BotOnMessageReceived();
        var callback = new BotOnCallbackQueryReceived();
        var notProcessed = new BotOnNotProcessedReceived();

        var handler = update.Type switch
        {
            UpdateType.Message => message.Handler(update.Message, botClient),
            UpdateType.CallbackQuery => callback.Handler(update.CallbackQuery, botClient),

            _ => notProcessed.Handler(update)
        };

        await handler;
    }
}