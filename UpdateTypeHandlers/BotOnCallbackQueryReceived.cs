using botTelegram.DateBase;
using botTelegram.Models;
using botTelegram.UpdateHandler;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using botTelegram.ExtensionMethods;

namespace botTelegram.UpdateTypeHandlers
{
    internal class BotOnCallbackQueryReceived
    {
        public async Task Handler(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            Console.WriteLine(callbackQuery.From.FirstName + " " + callbackQuery.From.LastName + "   |   " + callbackQuery.Data);

            if (!ExtensionMethods.ExtensionMethods.CheckUserInDb(callbackQuery.From.Id))
            {
                botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "В текущей версии бота, вы не авторизованы\nВведи имя под которым тебя многие узнают");
                return;
            }
            string[] callbackQueryData = callbackQuery.Data.Split(':');

            var callbackQueryHandler = callbackQueryData[0] switch
            {
                "changeNickname" => RetressOnChangeNick(callbackQuery, botClient),
                "changeAboutMe" => RetressOnChangeAboutMe(callbackQuery, botClient),
                "changePhoto" => RetressOnChangePhoto(callbackQuery, botClient),
                "guestsEvents" => SendGuestList(callbackQuery, botClient),
                "joinEvent" => RetressOnJoinEvent(callbackQuery, botClient),
                "leaveEvent" => RetressOnLeaveEvent(callbackQuery, botClient),
                "createEvent" => RetressOnCreatePassCheck(callbackQuery, botClient),
                "deleteEvent" => RetressOnDeletePassCheck(callbackQuery, botClient),
                "changeEventRank" => RetressOnChangeEventRank(callbackQuery, botClient),
                "choiceEventRank" => ChangeEventRank(callbackQuery, botClient),

                _ => Task.CompletedTask
            };

            await callbackQueryHandler;
        }


        async Task RetressOnChangeNick(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Пользователь меняет ник...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"На данный момент, все тебя знают как '{user.Nickname}'.\nОтправь мне свой новый никнейм.");

            user.SetStateAndSave(db, UserState.WaitingNick);
        }
        async Task RetressOnChangeAboutMe(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Пользователь меняет о себе...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"На данный момент о тебе:\n~~~ ~~~ ~~~\n{user.AboutMe}\n~~~ ~~~ ~~~\nОтправь мне новую информацию о себе.");

            user.SetStateAndSave(db, UserState.WaitingAboutMe);
        }
        async Task RetressOnChangePhoto(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Пользователь меняет фото...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Отправь мне свою новую фотку с разрешением не больше 512 х 512 пикселей.");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            user.SetStateAndSave(db, UserState.WaitingPhoto);
        }

        async Task SendGuestList(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            string[] callbackQueryData = callbackQuery.Data.Split(':');

            using var db = new BeerDbContext();
            var @event = db.Events.First(e => e.Id == long.Parse(callbackQueryData[2]));
            var listUsersEvents = db.Presences.Where(p => p.IdEvent == @event.Id);

            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"~~~ ~~~ ~~~\nСписок участников мероприятия {@event.Title}:");
            foreach (var t in listUsersEvents)
            {
                var user = db.Users.First(us => us.Id == t.IdUser);
                using (var fileStream = new FileStream(Path.GetFullPath(UpdateHendler.PathToPhoto(user.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await botClient.SendPhotoAsync(callbackQuery.Message.Chat.Id, photo: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream),
                        $"Имя: {user.Nickname}.\n" +
                        $"Статус: {t.Rank.ToLocaleString()}.\n" +
                        $"О себе:\n{user.AboutMe}.");
                }
            }
        }
        async Task RetressOnJoinEvent(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введи код мероприятия.");
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Сообщение отправленно...");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            user.SetStateAndSave(db, UserState.WaitingJoinCode);
        }
        async Task RetressOnLeaveEvent(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введи код мероприятия.");
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Сообщение отправленно...");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            user.SetStateAndSave(db, UserState.WainingLeaveCode);
        }
        async Task RetressOnCreatePassCheck(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Запрос на код...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите код подтверждающий что вы доверенное лицо.");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            user.SetStateAndSave(db, UserState.WaitingCreateEventCode);
        }
        async Task RetressOnDeletePassCheck(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Запрос на код...");

            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите код подтверждающий что вы доверенное лицо.");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            user.SetStateAndSave(db, UserState.WaitingDeleteEventCode);
        }
        async Task RetressOnChangeEventRank(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            string[] callbackQueryData = callbackQuery.Data.Split(':');
            string activeEventId = callbackQueryData[2];

            InlineKeyboardMarkup button = new(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Я пойду", $"choiceEventRank:{callbackQuery.From.Id}:{activeEventId}:Member")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Мне нужно подумать", $"choiceEventRank:{callbackQuery.From.Id}:{activeEventId}:Doubting")
            }
        });

            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Как ты настроен на это мероприятие.", replyMarkup: button);
        }
        async Task ChangeEventRank(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();

            string[] callbackQueryData = callbackQuery.Data.Split(':');
            var @event = db.Events.First(e => e.Id == long.Parse(callbackQueryData[2]));

            var mutableLink = db.Presences.First(p => p.IdUser == callbackQuery.From.Id && p.IdEvent == @event.Id);

            mutableLink.Rank = callbackQueryData[3] switch
            {
                "Member" => Rank.Member,
                "Doubting" => Rank.Doubting,

                _ => Rank.Invited
            };

            db.SaveChanges();

            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Теперь твой статус в {@event.Title} это {mutableLink.Rank.ToLocaleString()}");
            Console.WriteLine("Пользователь поменял статус на мероприятии...");
        }
    }
}
