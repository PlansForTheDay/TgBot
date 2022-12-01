using botTelegram.DateBase;
using botTelegram.Models;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;
using Telegram.Bot.Types;
using User = botTelegram.Models.User;
using botTelegram.ExtensionMethods;
using Update = Telegram.Bot.Types.Update;

namespace botTelegram.UpdateHandler;

public class UpdateHendler
{
    //=================================================================================================================
    static string PathToPhoto(long id)
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
    async static void SendListEvent(Presence presence, Message message, ITelegramBotClient botClient)
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

    //=================================================================================================================
    public async static Task UpdateHandlerAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
    {
        var botOnReceived = new UpdateHendler();
        var handler = update.Type switch
        {
            UpdateType.Message => botOnReceived.BotOnMessageReceived(update.Message, botClient),
            UpdateType.CallbackQuery => botOnReceived.BotOnCallbackQueryReceived(update.CallbackQuery, botClient),

            _ => botOnReceived.BotOnNotProcessedReceived(update)
        };

        await handler;
    }
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    async Task BotOnMessageReceived(Message message, ITelegramBotClient botClient)
    {
        Console.WriteLine(message.Chat.FirstName + " " + message.Chat.LastName + "   |   " + message.Text);

        if (!ExtensionMethods.ExtensionMethods.RegistrationCheck(botClient, message))
            return;

        var messageHandler = message.Type switch
        {
            MessageType.Text => TextHandler(message, botClient),
            MessageType.Photo => PhotoHandler(message, botClient),
            MessageType.Sticker => StickerHandler(message, botClient),
            MessageType.VideoNote => VideoNoteHandler(message, botClient),

            _ => Task.CompletedTask
        };
        await messageHandler;
    }
    //-----------------------------------------------------------------------------------------------------------------
    async Task TextHandler(Message message, ITelegramBotClient botClient)
    {
        if (message.Text[0] == '/')
        {
            var textCommandHendler = message.Text switch
            {
                "/help" => HelpTextCommand(message, botClient),
                "/me" => MeTextCommand(message, botClient),
                "/events" => EventsTextCommand(message, botClient),
                "/back" => BackTextCommand(message, botClient),

                _ => botClient.SendTextMessageAsync(message.Chat.Id, "Такого не знаем(.")
            };

            await textCommandHendler;
            return;
        }

        using var db = new BeerDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);
        string password = "312fox";

        var textHendler = user.State switch
        {
            UserState.WaitingNick => ChangeNick(message, botClient),
            UserState.WaitingJoinCode => JoinEvent(message, botClient),
            UserState.WaitingAboutMe => ChangeAboutMe(message, botClient),
            UserState.WaitingCreateEventCode => CreatePassCheck(message, botClient, password),
            UserState.WaitingInfoCreateEvent => CreateEvent(message, botClient),
            UserState.WainingLeaveCode => LeaveEvent(message, botClient),
            UserState.WaitingDeleteEventCode => DeletePassCheck(message, botClient, password),
            UserState.WaitingInfoDeleteEvent => DeleteEvent(message, botClient),

            _ => botClient.SendTextMessageAsync(message.From.Id, "Ну и что ты только что сказал?\nТы в меню и я не жду от тебя сообщения.")
        };

        await textHendler;
    }


    async Task HelpTextCommand(Message message, ITelegramBotClient botClient)
    {
        Console.WriteLine(message.Chat.FirstName + " | Пользователь узнаёт команды...");
        await botClient.SendTextMessageAsync(message.Chat.Id, "Вот список команд:\n" +
                        "| /help - Показывает все нужные команды.\n" +
                        "| /me - Показывает информацию о вас.\n" +
                        "| /events - Взаимодействие с мероприятиями.\n" +
                        "| /back - Прекращает текущее действие.");
    }
    async Task MeTextCommand(Message message, ITelegramBotClient botClient)
    {
        Console.WriteLine(message.Chat.FirstName + " | Пользователь решил узнать информацию о себе...");

        using (BeerDbContext db = new BeerDbContext())
        {
            User user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);

            InlineKeyboardMarkup button = new(new[]
            {
                new[]
                        {
                    InlineKeyboardButton.WithCallbackData("Изменить имя", $"changeNickname:{user.Id}")
                },
                new[]
                        {
                    InlineKeyboardButton.WithCallbackData("Изменить о себе", $"changeAboutMe:{user.Id}")
                },
                new[]
                        {
                    InlineKeyboardButton.WithCallbackData("Изменить фото", $"changePhoto:{user.Id}")
                }
            });

            using (var fileStream = new FileStream(Path.GetFullPath(PathToPhoto(user.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await botClient.SendPhotoAsync(message.Chat.Id, photo: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream),
                //$"Информация о пользователе: \n" +
                $"| Ваше имя - {user.Nickname}.\n" +
                $"| Ваш ID - {user.Id}.\n" +
                $"| О вас -\n {user.AboutMe}.", replyMarkup: button);
            }
            user.SetStateAndSave(db, UserState.Menu);
        }
    }
    async Task EventsTextCommand(Message message, ITelegramBotClient botClient)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Вот мероприятия на которые ты планируешь прийти и панель взаимодействия с ними:");
        try
        {
            await using (BeerDbContext db = new BeerDbContext())
            {
                User user = db.Users.First(u => u.Id == message.From.Id);

                IQueryable<Presence> presence = db.Presences.Where(e => e.IdUser == user.Id);
                if (presence != null)
                {
                    Console.WriteLine("Пользователь просмотривает мероприятия...");

                    foreach (var i in presence)
                    {
                        SendListEvent(i, message, botClient);
                    }
                }
                else
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Ой, тебя нет ни на одном мероприятии.");

                InlineKeyboardMarkup button = new(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Присоединиться", $"joinEvent:{user.Id}")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Покинуть", $"leaveEvent:{user.Id}")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Создать", $"createEvent:{user.Id}"),
                        InlineKeyboardButton.WithCallbackData("Удалить", $"deleteEvent:{user.Id}")
                    }
                });

                await botClient.SendTextMessageAsync(message.Chat.Id, "Взаимодействие с мероприятиями.", replyMarkup: button);

                Console.WriteLine(message.Chat.FirstName + " | Сообщение отправленно...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка...\n" + ex);
        }
    }
    async Task BackTextCommand(Message message, ITelegramBotClient botClient)
    {
        using (BeerDbContext db = new BeerDbContext())
        {
            Console.WriteLine(message.Chat.FirstName + " | Возврат в меню...");
            await botClient.SendTextMessageAsync(message.From.Id, "Вы вернулись в меню");           //желательно сделать всплывающим сообщением а не обычным

            User user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);
            user.SetStateAndSave(db, UserState.Menu);
        }
    }

    async Task ChangeNick(Message message, ITelegramBotClient botClient)
    {
        using (BeerDbContext db = new BeerDbContext())
        {
            var user = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
            user.Nickname = message.Text;
            user.State = UserState.Menu;
            db.SaveChanges();

            await botClient.SendTextMessageAsync(message.Chat.Id, $"Теперь все тебя знают как '{user.Nickname}'.");
            Console.WriteLine(message.Chat.FirstName + " | Пользователь поменял ник...");
        }
    }
    async Task JoinEvent(Message message, ITelegramBotClient botClient)
    {
        try
        {
            using var db = new BeerDbContext();

            var @event = db.Events.FirstOrDefault(x => x.Code == message.Text);
            if (@event == null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятия с таким кодом нет.");
                Console.WriteLine(message.Chat.FirstName + " | Неверный код мероприятия...");
                return;
            }

            User user = db.Users.FirstOrDefault(i => i.Id == message.From.Id);
            var w = new Presence(user, @event);

            var eventMembership = db.Presences.FirstOrDefault(i => i.IdUser == user.Id && i.IdEvent == @event.Id);
            if (eventMembership == null)
            {
                db.Presences.Add(w);
                db.SaveChanges();

                await botClient.SendTextMessageAsync(message.Chat.Id, "Вы присоединились к мероприятию как приглашённый.");
            }
            else
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Вы уже состоите в списке гостей этого мероприятия.");
            }
            user.SetStateAndSave(db, UserState.Menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка...\n" + ex);
        }
    }
    async Task ChangeAboutMe(Message message, ITelegramBotClient botClient)
    {
        Console.WriteLine(message.Chat.FirstName + " | Пользователь поменял о себе...");

        using var db = new BeerDbContext();

        User user = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
        user.AboutMe = message.Text;
        db.SaveChanges();

        await botClient.SendTextMessageAsync(message.Chat.Id, $"Вот что получилось:\n '{user.AboutMe}'.");

        user.SetStateAndSave(db, UserState.Menu);
    }
    async Task CreatePassCheck(Message message, ITelegramBotClient botClient, string password)
    {
        using var db = new BeerDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);
        if (!message.Text.Contains(password))
        {
            Console.WriteLine(message.Chat.FirstName + " | Пользователь не ввёл код...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Вы ввели неверный код, возврат в меню.");

            user.SetStateAndSave(db, UserState.Menu);
        }

        Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл верный код...");
        await botClient.SendTextMessageAsync(message.Chat.Id, "Верно.");
        await botClient.SendTextMessageAsync(message.Chat.Id, "Теперь введи основную информацию следующим образом:\n(код придумай уникальный и запомни)\n \n" +
            "[название]\n" +
            "[дату]\n" +
            "[код мероприятия]\n \n" +
            "Сообщение должно выглядеть примерно вот так:");
        await botClient.SendTextMessageAsync(message.Chat.Id, "Китайский новый год\n" +
            "3.9.2077\n" +
            "DavayMn3Sv0yC0d");

        user.SetStateAndSave(db, UserState.WaitingInfoCreateEvent);
    }
    async Task CreateEvent(Message message, ITelegramBotClient botClient)
    {
        Console.WriteLine(message.Chat.FirstName + " | Создание мероприятия...");

        string[] words = message.Text.Split('\n');
        int[] dateParts = new int[words[1].Length];

        try
        {
            dateParts = words[1].Split('.').Select(x => int.Parse(x)).ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка...\n" + ex);
            await botClient.SendTextMessageAsync(message.Chat.Id, "Видимо вы некорректно ввели информацию.\nПопробуйте ещё раз");
            return;
        }

        string eventCode = words[2];
        string eventTitle = words[0];
        DateTime eventDate = new DateTime(dateParts[2], dateParts[1], dateParts[0]);

        try
        {
            Event @event = new Event(eventTitle, eventDate, eventCode);
            User user;

            await using (BeerDbContext db = new BeerDbContext())
            {
                db.Events.Add(@event);

                user = db.Users.First(i => i.Id == message.From.Id);
                var newPresece = new Presence(user, @event);
                newPresece.Rank = Rank.Administrator;

                db.Presences.Add(newPresece);

                db.SaveChanges();
                user.SetStateAndSave(db, UserState.Menu);
            }

            Console.WriteLine("Мероприятие создано...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие создано.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка...\n" + ex);
            await botClient.SendTextMessageAsync(message.Chat.Id, "Ошибка.");
        }
    }
    async Task LeaveEvent(Message message, ITelegramBotClient botClient)
    {
        using var db = new BeerDbContext();

        var even = db.Events.FirstOrDefault(x => x.Code == message.Text);
        if (even == null)
        {
            Console.WriteLine(message.Chat.FirstName + " | Неверный код мероприятия...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятия с таким кодом нет.");
            return;
        }
        Console.WriteLine(message.Chat.FirstName + " | Верный код мероприятия...");

        var user = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
        var w = db.Presences.FirstOrDefault(t => t.IdUser == user.Id && t.IdEvent == even.Id);
        if (w != null)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Вы покинули мероприятие.");
            db.Presences.Remove(w);
            db.SaveChanges();
        }
        else
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Вы не участвовали в этом мероприятии.");
        }
        user.SetStateAndSave(db, UserState.Menu);
    }
    async Task DeletePassCheck(Message message, ITelegramBotClient botClient, string password)
    {
        using var db = new BeerDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);

        if (!message.Text.Contains(password))
        {
            Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл неверный код...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Неверный код. Возврат в меню.");

            user.SetStateAndSave(db, UserState.Menu);
            return;
        }

        Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл верный код...");
        await botClient.SendTextMessageAsync(message.Chat.Id, "Верно.");
        await botClient.SendTextMessageAsync(message.Chat.Id, "Теперь укажи код мероприятия.");

        user.SetStateAndSave(db, UserState.WaitingInfoDeleteEvent);
    }
    async Task DeleteEvent(Message message, ITelegramBotClient botClient)
    {
        using var db = new BeerDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);
        try
        {
            Event @event = db.Events.First(q => q.Code == message.Text);
            if (@event == null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие не найдено, возврат в меню.");
                return;
            }
            Presence presence = db.Presences.FirstOrDefault(p => p.IdUser == user.Id && p.IdEvent == @event.Id);
            if (presence == null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Вы не числитесь на этом мероприятии.");
                Console.WriteLine("Отказ по причине отсутствия на мероприятии...");
                return;
            }

            db.Events.Remove(@event);
            db.SaveChanges();

            Console.WriteLine("Мероприятие удалено...");
            await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие удалено.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка...\n" + ex);
            await botClient.SendTextMessageAsync(message.Chat.Id, "Ошибка.");
        }
        user.SetStateAndSave(db, UserState.Menu);
    }
    //`````````````````````````````````````````````````````````````````````````````````````````````````````````````````
    async Task PhotoHandler(Message message, ITelegramBotClient botClient)
    {
        using var db = new BeerDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == message.From.Id);
        if (!Directory.Exists("user_photo"))
        {
            await botClient.SendTextMessageAsync(message.From.Id, "Возникли технические шоколадки и пока что у меня нет возможности работать с фото.");
            Console.WriteLine("Директории с видео не обнаружено...");
            user.SetStateAndSave(db, UserState.Menu);
            return;
        }

        var photoHanler = user.State switch
        {
            UserState.WaitingPhoto => ChangePhoto(message, botClient),

            _ => Task.CompletedTask
        };
    }


    async Task ChangePhoto(Message message, ITelegramBotClient botClient)
    {
        User user;
        using var db = new BeerDbContext();
        user = db.Users.First(u => u.Id == message.From.Id);

        bool chek = false;
        for (int i = 0; i < message.Photo.Length; i++)
        {
            if (message.Photo[i].Width < 513 && message.Photo[i].Height < 513)
            {
                var fileId = message.Photo.Last().FileId;
                var fileInfo = await botClient.GetFileAsync(fileId);
                var filePath = fileInfo.FilePath;

                string destinationFilePath = $"user_photo\\{user.Id}.png";
                await using (FileStream fileStream = System.IO.File.OpenWrite(destinationFilePath))
                {
                    await botClient.DownloadFileAsync(filePath: filePath, destination: fileStream);
                }
                chek = true;
            }
        }

        if (!chek)
        {
            await botClient.SendTextMessageAsync(message.From.Id, "Размеры не подходят.");
            return;
        }
        await botClient.SendTextMessageAsync(message.From.Id, "Фото обновлено.");
        Console.WriteLine($"Пользователь {user.Nickname} поменял фото...");
        user.SetStateAndSave(db, UserState.Menu);
    }
    //`````````````````````````````````````````````````````````````````````````````````````````````````````````````````
    async Task StickerHandler(Message message, ITelegramBotClient botClient)
    {
        try
        {
            string pathToStickers = "stickers";
            string[] listStickers;
            Random randomSticker = new Random();

            if (!Directory.Exists(pathToStickers))
            {
                await botClient.SendTextMessageAsync(message.From.Id, "Возникли технические шоколадки и пока что у меня нет возможности ответить как надо.");
                Console.WriteLine("Директории со стикерами не обнаружено...");
                return;
            }
            listStickers = Directory.GetFiles(pathToStickers, "*.web?");      //"*.webp" , "*.webm"
            int directorySize = listStickers.Length;

            string sticker = listStickers[randomSticker.Next(0, directorySize)];

            if (sticker.Contains(".webm"))
            {
                using (var fileStream = new FileStream(Path.GetFullPath(sticker), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await botClient.SendVideoAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream));
                }
            }
            if (sticker.Contains(".webp"))
            {
                using (var stick = System.IO.File.Open(sticker, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await botClient.SendStickerAsync(message.From.Id, stick);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка...\n" + ex);
        }
    }
    //`````````````````````````````````````````````````````````````````````````````````````````````````````````````````
    async Task VideoNoteHandler(Message message, ITelegramBotClient botClient)
    {
        if (!Directory.Exists("video"))
        {
            await botClient.SendTextMessageAsync(message.From.Id, "Возникли технические шоколадки и пока что у меня нет возможности ответить как надо.");
            Console.WriteLine("Директории с видео не обнаружено...");
            return;
        }

        string path = "video\\hmmm.mp4";

        using (var fileStream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await botClient.SendVideoNoteAsync(message.Chat.Id, videoNote: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream));
        }
        Console.WriteLine("Пользователь отправил кружок...");
    }
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery, ITelegramBotClient botClient)
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
            "choiceEventRank" => ChangeEventRank(callbackQuery,botClient),

            _ => Task.CompletedTask
        };

        await callbackQueryHandler;
    }
    //-----------------------------------------------------------------------------------------------------------------
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
            using (var fileStream = new FileStream(Path.GetFullPath(PathToPhoto(user.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
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
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    async Task BotOnNotProcessedReceived(Update update)
    {
        Console.WriteLine("Необрабатываемый Update:\n ID\n  =>" + update.Id + "\n Type\n  =>" + update.Type);
    }
}