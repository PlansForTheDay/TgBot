using botTelegram.DateBase;
using botTelegram.Models;
using botTelegram.UpdateHandler;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using botTelegram.ExtensionMethods;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text;

namespace botTelegram.UpdateTypeHandlers
{
    internal class BotOnCallbackQueryReceived
    {
        static async Task NoProcessedButton(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            await botClient.SendTextMessageAsync(callbackQuery.From.Id, "Неизвестная кнопка.");
            Console.Write("Ошибка определения кнопки...");
        }

        public static async Task Handler(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            Console.WriteLine(callbackQuery.From.FirstName + " " + callbackQuery.From.LastName + "   |   " + callbackQuery.Data);

            if (!ExtensionMethods.ExtensionMethods.CheckUserInDb(callbackQuery.From.Id))
            {
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "В текущей версии бота, вы не авторизованы\nВведи имя под которым тебя многие узнают");
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
                "EventCreate" => EventInformationRequest(callbackQuery, botClient),
                "EventDelete" => EventCodeRequest(callbackQuery, botClient),
                "changeEventRank" => RetressOnChangeEventRank(callbackQuery, botClient),
                "choiceEventRank" => ChangeEventRank(callbackQuery, botClient),
                "adminButtons" => DisplayAdminButtons(callbackQuery, botClient),


                "changeTitleEvent" => RetressOnChangeEvent(callbackQuery, botClient),
                "changeLocationEvent" => RetressOnChangeEvent(callbackQuery, botClient),
                "changeDateEvent" => RetressOnChangeEvent(callbackQuery, botClient),
                "changeTimeEvent" => RetressOnChangeEvent(callbackQuery, botClient),
                //"appointAdmin"
                //"removeGuest"
                 

                _ => NoProcessedButton(callbackQuery, botClient)
            };

            await callbackQueryHandler;
        }


        static async Task RetressOnChangeNick(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Пользователь меняет ник...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"На данный момент, все тебя знают как '{user.Nickname}'.\nОтправь мне свой новый никнейм.");

            user.SetStateAndSave(db, UserState.WaitingNick);
        }
        static async Task RetressOnChangeAboutMe(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Пользователь меняет о себе...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"На данный момент о тебе:\n~~~ ~~~ ~~~\n{user.AboutMe}\n~~~ ~~~ ~~~\nОтправь мне новую информацию о себе.");

            user.SetStateAndSave(db, UserState.WaitingAboutMe);
        }
        static async Task RetressOnChangePhoto(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Пользователь меняет фото...");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Отправь мне свою новую фотку с разрешением не больше 512 х 512 пикселей.");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            user.SetStateAndSave(db, UserState.WaitingPhoto);
        }

        static async Task SendGuestList(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            string[] callbackQueryData = callbackQuery.Data.Split(':');

            using var db = new BeerDbContext();
            var @event = db.Events.First(e => e.Id == long.Parse(callbackQueryData[2]));
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"~~~ ~~~ ~~~\nСписок участников мероприятия {@event.Title}:");

            var listUsersEvents = db.Presences.Where(p => p.IdEvent == @event.Id);
            foreach (var thisPresence in listUsersEvents)
            {
                var user = db.Users.First(u => u.Id == thisPresence.IdUser);
                using (var fileStream = new FileStream(Path.GetFullPath(UpdateHendler.PathToPhoto(user.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await botClient.SendPhotoAsync(callbackQuery.Message.Chat.Id, photo: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream),
                        $"Имя: {user.Nickname}.\n" +
                        $"Статус: {thisPresence.Rank.ToLocaleString()}.\n" +
                        $"О себе:\n{user.AboutMe}.");
                }
            }
        }
        static async Task RetressOnJoinEvent(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введи код мероприятия.");
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Сообщение отправленно...");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            user.SetStateAndSave(db, UserState.WaitingJoinCode);
        }
        static async Task RetressOnLeaveEvent(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введи код мероприятия.");
            Console.WriteLine(callbackQuery.Message.Chat.FirstName + " | Сообщение отправленно...");

            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            user.SetStateAndSave(db, UserState.WainingLeaveCode);
        }
        static async Task EventInformationRequest(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введи основную информацию следующим образом:\n(код придумай уникальный и запомни)\n \n" +
                "[название]\n" +
                "[дату]\n" +
                "[код мероприятия]\n \n" +
                "Сообщение должно выглядеть примерно вот так:");
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id,
                "Китайский новый год\n" +
                "3.9.2077\n" +
                "DavayMn3Sv0yC0d");

            user.SetStateAndSave(db, UserState.WaitingInfoCreateEvent);
        }
        static async Task EventCodeRequest(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);

            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Укажи код мероприятия.");

            user.SetStateAndSave(db, UserState.WaitingRemovedEventCode);
        }
        static async Task RetressOnChangeEventRank(CallbackQuery callbackQuery, ITelegramBotClient botClient)
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
        static async Task ChangeEventRank(CallbackQuery callbackQuery, ITelegramBotClient botClient)
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
        static async Task DisplayAdminButtons(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            string[] callbackQueryData = callbackQuery.Data.Split(':');

            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            var @event = db.Events.First(e => e.Id == long.Parse(callbackQueryData[2]));

            InlineKeyboardMarkup button = new(new[]
            {
                new[] 
                {
                    InlineKeyboardButton.WithCallbackData("Название", $"changeTitleEvent:{user.Id}:{@event.Id}"),
                    InlineKeyboardButton.WithCallbackData("Локацию", $"changeLocationEvent:{user.Id}:{@event.Id}")
                },
                new[] 
                {
                    InlineKeyboardButton.WithCallbackData("Дату", $"changeDateEvent:{user.Id}:{@event.Id}"),
                    InlineKeyboardButton.WithCallbackData("Время", $"changeTimeEvent:{user.Id}:{@event.Id}")
                },
                new[] {InlineKeyboardButton.WithCallbackData("Назначить администратора", $"appointAdmin:{user.Id}:{@event.Id}")},
                new[] {InlineKeyboardButton.WithCallbackData("Отстранить гостя", $"removeGuest:{user.Id}:{@event.Id}")}
            });

            await botClient.SendTextMessageAsync(callbackQuery.From.Id, $"Команды изменения мероприятия: {@event.Title}.\nЧто нужно изменить?", replyMarkup: button);
        }
        static async Task RetressOnChangeEvent(CallbackQuery callbackQuery, ITelegramBotClient botClient)
        {
            using var db = new BeerDbContext();
            string[] callbackQueryData = callbackQuery.Data.Split(':');
            var user = db.Users.First(u => u.Id == callbackQuery.From.Id);
            var @event = db.Events.First(e => e.Id == long.Parse(callbackQueryData[2]));

            string eventParameter = callbackQueryData[0] switch
            {
                "changeTitleEvent" => $"{@event.Id}.Title",
                "changeLocationEvent" => $"{@event.Id}.Location",
                "changeDateEvent" => $"{@event.Id}.Date",
                "changeTimeEvent" => $"{@event.Id}.Time",

                _ => "NoProcessedButton"
            };

            if (eventParameter == "NoProcessedButton")
            {
                await NoProcessedButton(callbackQuery, botClient);
                user.SetStateAndSave(db, UserState.Menu);
                return;
            }

            string path = $"event_change_files\\{user.Id}.txt";

            using (FileStream fileStream = System.IO.File.Create(path, 1024))
            {
                byte[] parameter = new UTF8Encoding(true).GetBytes(eventParameter);
                fileStream.Write(parameter, 0, parameter.Length);
            }

            var chatInstruction = callbackQueryData[0] switch
            {
                "changeTitleEvent" => await botClient.SendTextMessageAsync(callbackQuery.From.Id, $"Введи новое название мероприятия \n{@event.Title}"),
                "changeLocationEvent" => await botClient.SendTextMessageAsync(callbackQuery.From.Id, $"Введи новую локацию мероприятия \n{@event.Title}"),
                "changeDateEvent" => await botClient.SendTextMessageAsync(callbackQuery.From.Id, $"Введи новую дату мероприятия \n{@event.Title}\nПример: [12.2.1981]"),
                "changeTimeEvent" => await botClient.SendTextMessageAsync(callbackQuery.From.Id, $"Введи новое время мероприятия \n{@event.Title}\nПример: [15:30]")
            };

            user.SetStateAndSave(db, UserState.WaitingChangeEvent);
        }
    }
}