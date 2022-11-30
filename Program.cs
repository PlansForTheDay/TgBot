using botTelegram.DateBase;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using botTelegram.Models;
using User = botTelegram.Models.User;

namespace Bot
{

    class Program
    {
        static void Main()
        {
            var client = new TelegramBotClient("5774258095:AAGdzDdOjKogupeTr-9Dto-MGZ1QJGATTrA");

            client.StartReceiving(UpdateHandler, ErrorHandler);
            string stop = "";
            while (!stop.ToLower().Contains("/stop"))
            {
                stop = Console.ReadLine();
            }
        }

        async static Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            string password = "tomatoday";
            //var message = update.Message;

            var handler = update switch
            {
                { Message: { } message }                => BotOnMessageReceived(message),


                _ => 1
            };

            await handler;

            async Task BotOnMessageReceived(Message message)
            {
                Console.WriteLine(message.Chat.FirstName + " " + message.Chat.LastName + "   |   " + message.Text);

                User ch;
                using (BeerDbContext db = new BeerDbContext())
                {
                    ch = db.Users.FirstOrDefault(x => x.Id == message.From.Id);
                }
                if (ch == null)
                {
                    if (!message.Text.Contains("/"))
                    {
                        try
                        {
                            string nickname = message.Text;
                            User one = new User(message.From, nickname);

                            using (BeerDbContext db = new BeerDbContext())
                            {
                                db.Users.Add(one);
                                db.SaveChanges();
                            }

                            Console.WriteLine("Пользователь зарегистрирован без ошибок...\n");
                            await botClient.SendTextMessageAsync(message.Chat.Id, $"Теперь ты зарегистрирован и можешь взаимодействовать с ботом\n" +
                                $"Тебя зовут {message.Text}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Ошибка...\n" + ex);
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "В текущей версии бота, вы не авторизованы\n" +
                            "Введи имя под которым тебя многие узнают");

                        Console.WriteLine(message.Chat.FirstName + " | Запрос на имя отправлен...");
                    }
                }
                else
                {
                    User userInfo;
                    using (BeerDbContext db = new BeerDbContext())
                    {
                        userInfo = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
                    }
                    if (message.Text != null)
                    {
                        if (message.Text[0] == '/')
                        {
                            switch (message.Text)
                            {
                                case "/help":

                                    Console.WriteLine(message.Chat.FirstName + " | Пользователь узнаёт команды...");

                                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вот список команд:\n" +
                                        "| /help - Показывает все нужные команды.\n" +
                                        "| /me - Показывает информацию о вас.\n" +
                                        "| /events - Взаимодействие с мероприятиями.\n" +
                                        "| /back - Прекращает текущее действие.");

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case "/me":

                                    Console.WriteLine(message.Chat.FirstName + " | Пользователь решил узнать информацию о себе...");

                                    InlineKeyboardMarkup button1 = new(new[]
                                    {
                                            new []
                                            {
                                                InlineKeyboardButton.WithCallbackData("Изменить имя", $"changeNickname:{userInfo.Id}")
                                            },
                                            new []
                                            {
                                                InlineKeyboardButton.WithCallbackData("Изменить о себе", $"changeAboutMe:{userInfo.Id}")
                                            },
                                            new []
                                            {
                                                InlineKeyboardButton.WithCallbackData("Изменить фото", $"changePhoto:{userInfo.Id}")
                                            }
                                        });

                                    using (var fileStream = new FileStream(Path.GetFullPath(PathToPhoto(userInfo.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        await botClient.SendPhotoAsync(message.Chat.Id, photo: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream),
                                        //$"Информация о пользователе: \n" +
                                        $"| Ваше имя - {userInfo.Nickname}.\n" +
                                        $"| Ваш ID - {userInfo.Id}.\n" +
                                        $"| О вас -\n {userInfo.AboutMe}.", replyMarkup: button1);
                                    }
                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case "/back":

                                    Console.WriteLine(message.Chat.FirstName + " | Возврат в меню...");

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case "/events":

                                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вот мероприятия на которые ты планируешь прийти и панель взаимодействия с ними:");
                                    try
                                    {
                                        await using (BeerDbContext db = new BeerDbContext())
                                        {
                                            IQueryable<Presence> presence = db.Presences.Where(e => e.IdUser == userInfo.Id);
                                            if (presence != null)
                                            {
                                                Console.WriteLine("Пользователь просмотривает мероприятия...");

                                                foreach (var i in presence)
                                                {
                                                    ChatEvent(i, message, botClient);
                                                }
                                            }
                                            else
                                            {
                                                await botClient.SendTextMessageAsync(message.Chat.Id, "Ой, тебя нет ни на одном мероприятии.");
                                                Console.WriteLine("Пользователь никуда не хочет идти...");
                                            }
                                        }

                                        InlineKeyboardMarkup button2 = new(new[]
                                        {
                                                new []
                                                {
                                                    InlineKeyboardButton.WithCallbackData("Присоединиться", $"joinEvent:{userInfo.Id}")
                                                },
                                                new []
                                                {
                                                    InlineKeyboardButton.WithCallbackData("Покинуть", $"leaveEvent:{userInfo.Id}")
                                                },
                                                new []
                                                {
                                                    InlineKeyboardButton.WithCallbackData("Создать", $"createEvent:{userInfo.Id}"),
                                                    InlineKeyboardButton.WithCallbackData("Удалить", $"deleteEvent:{userInfo.Id}")
                                                }
                                            });

                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Взаимодействие с мероприятиями.", replyMarkup: button2);

                                        Console.WriteLine(message.Chat.FirstName + " | Сообщение отправленно...");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Ошибка...\n" + ex);
                                    }
                                    break;


                                default:

                                    await botClient.SendTextMessageAsync(message.Chat.Id, "И что ты только что сказал?");
                                    Console.WriteLine(message.Chat.FirstName + " | Сообщение отправленно...");

                                    break;
                            }
                        }
                        else
                        {
                            switch (userInfo.State)
                            {
                                case UserState.waitingNick:

                                    Console.WriteLine(message.Chat.FirstName + " | Пользователь поменял ник...");

                                    using (BeerDbContext db = new BeerDbContext())
                                    {
                                        User user = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
                                        user.Nickname = message.Text;
                                        user.State = 0;

                                        db.SaveChanges();

                                        await botClient.SendTextMessageAsync(message.Chat.Id, $"Теперь все тебя знают как '{user.Nickname}'.");
                                    }

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case UserState.waitingJoinCode:

                                    JoinEvent(message, botClient);

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case UserState.waitingAboutMe:
                                    Console.WriteLine(message.Chat.FirstName + " | Пользователь поменял о себе...");

                                    using (BeerDbContext db = new BeerDbContext())
                                    {
                                        User user = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
                                        user.AboutMe = message.Text;

                                        db.SaveChanges();

                                        await botClient.SendTextMessageAsync(message.Chat.Id, $"Вот что получилось:\n" +
                                                $" '{user.AboutMe}'.");
                                    }

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case UserState.waitingCreateCode:

                                    if (message.Text.Contains(password))
                                    {
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

                                        StatusCorrector(message.From.Id, 5);
                                    }
                                    else
                                    {
                                        Console.WriteLine(message.Chat.FirstName + " | Пользователь не ввёл код...");
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Вы ввели неверный код возврат в меню.");

                                        StatusCorrector(message.From.Id, 0);
                                    }
                                    break;


                                case UserState.waitingEInfo:

                                    Console.WriteLine(message.Chat.FirstName + " | Создание мероприятия...");

                                    string[] words = message.Text.Split('\n');
                                    int[] da = new int[words[1].Length];

                                    try
                                    {
                                        da = words[1].Split('.').Select(x => int.Parse(x)).ToArray();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Ошибка...\n" + ex);
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Видимо вы некорректно ввели информацию.\nПопробуйте ещё раз");
                                        break;
                                    }

                                    string cod = words[2];
                                    string titl = words[0];
                                    DateTime dat = new DateTime(da[2], da[1], da[0]);

                                    try
                                    {
                                        Event even = new Event(titl, dat, cod, message.From);

                                        await using (BeerDbContext db = new BeerDbContext())
                                        {
                                            db.Events.Add(even);

                                            User user = db.Users.FirstOrDefault(i => i.Id == message.From.Id);
                                            var w = new Presence(user, even);
                                            db.Presences.Add(w);

                                            db.SaveChanges();
                                        }


                                        ChangeEventRank(even, message, 0);

                                        Console.WriteLine("Мероприятие создано...");

                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие создано.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Ошибка...\n" + ex);
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Ошибка.");
                                    }

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case UserState.wainingLeaveCode:

                                    LeaveEvent(message, botClient);

                                    StatusCorrector(message.From.Id, 0);
                                    break;


                                case UserState.waitingDeleteCode:

                                    if (message.Text.Contains(password))
                                    {
                                        Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл верный код...");
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Верно.");
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Теперь укажи код мероприятия.");

                                        StatusCorrector(message.From.Id, 8);
                                    }
                                    else
                                    {
                                        Console.WriteLine(message.Chat.FirstName + " | Пользователь ввёл неверный код...");
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Неверный код. Возврат в меню.");

                                        StatusCorrector(message.From.Id, 0);
                                    }
                                    break;


                                case UserState.deleteEvent:

                                    try
                                    {
                                        using (BeerDbContext db = new BeerDbContext())
                                        {
                                            Event even = db.Events.First(q => q.Code == message.Text);
                                            if (even != null)
                                            {
                                                DeleteEvent(even);
                                            }


                                            Console.WriteLine("Мероприятие удалено...");
                                        }
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятие удалено.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Ошибка...\n" + ex);
                                        await botClient.SendTextMessageAsync(message.Chat.Id, "Ошибка.");
                                    }

                                    StatusCorrector(message.From.Id, 0);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (message.Photo != null)
                        {
                            switch (userInfo.State)
                            {
                                case UserState.waitingPhoto:

                                    bool chek = false;
                                    for (int i = 0; i < message.Photo.Length; i++)
                                    {
                                        if (message.Photo[i].Width < 513 && message.Photo[i].Height < 513)
                                        {
                                            var fileId = update.Message.Photo.Last().FileId;
                                            var fileInfo = await botClient.GetFileAsync(fileId);
                                            var filePath = fileInfo.FilePath;

                                            string destinationFilePath = $"user_photo\\{userInfo.Id}.png";
                                            await using (FileStream fileStream = System.IO.File.OpenWrite(destinationFilePath))
                                            {
                                                await botClient.DownloadFileAsync(filePath: filePath, destination: fileStream);
                                            }
                                            chek = true;
                                        }
                                    }

                                    await botClient.SendTextMessageAsync(message.From.Id, "Фото обновлено.");

                                    if (!chek)
                                        await botClient.SendTextMessageAsync(message.From.Id, "Размеры не подходят.");
                                    Console.WriteLine($"Пользователь {userInfo.Nickname} поменял фото...");
                                    break;


                                default:
                                    string strin = "";
                                    for (int i = 0; i < message.Photo.Length; i++)
                                    {
                                        strin += $"Разрешение {i + 1}: {message.Photo[i].Width} x {message.Photo[i].Height}.\n";
                                    }

                                    await botClient.SendTextMessageAsync(message.Chat.Id, "А мне нравится.\n" + strin);

                                    Console.WriteLine("Пользователь отправил фото без контекста...");
                                    break;
                            }

                        }
                        if (message.Sticker != null)
                        {
                            Console.WriteLine("Пользователь отправил стикер...");

                            PrintRandSticker(botClient, message.From.Id);
                        }
                        if (message.VideoNote != null)
                        {
                            string path = "video\\hmmm.mp4";

                            using (var fileStream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                await botClient.SendVideoNoteAsync(message.Chat.Id, videoNote: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream));
                            }
                            Console.WriteLine("Пользователь отправил кружок...");
                        }
                    }
                }
            }


            switch (update.Type)
            {
                case UpdateType.Message:
                    
                    
                    break;
                    

                case UpdateType.CallbackQuery:

                    var callback = update.CallbackQuery;
                    User userCall;
                    string[] temp = callback.Data.Split(':');
                    using (BeerDbContext db = new BeerDbContext())
                    {
                        userCall = db.Users.FirstOrDefault(db => db.Id == long.Parse(temp[1]));
                    }

                    if (userCall != null)
                        Console.WriteLine($"Пользователь | {userCall.Nickname} | нажал кнопку...");
                    else
                        Console.WriteLine("Нажата кнопрка мероприятия...");

                    switch (temp[0])
                    {
                        case "changeNickname":
                            Console.WriteLine(callback.Message.Chat.FirstName + " | Пользователь меняет ник...");


                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, $"На данный момент, все тебя знают как '{userCall.Nickname}'.\n" +
                                $"Отправь мне свой новый никнейм.");

                            StatusCorrector(userCall.Id, 1);
                            break;


                        case "changeAboutMe":
                            Console.WriteLine(callback.Message.Chat.FirstName + " | Пользователь меняет о себе...");

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, $"На данный момент о тебе:\n" +
                                $" '{userCall.AboutMe}'.\n" +
                                $"  Отправь мне новую информацию о себе.");

                            StatusCorrector(userCall.Id, 3);
                            break;


                        case "changePhoto":
                            Console.WriteLine(callback.Message.Chat.FirstName + " | Пользователь меняет фото...");

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, $"Отправь мне свою новую фотку с разрешением не больше 512 х 512 пикселей.");

                            StatusCorrector(userCall.Id, 9);
                            break;


                        case "guestsEvents":
                            Event eve;
                            long id = long.Parse(temp[1]);

                            using (BeerDbContext db = new BeerDbContext())
                            {
                                eve = db.Events.FirstOrDefault(e => e.Id == id);
                            }

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, $"~~~ ~~~ ~~~\n" +
                                $"Список участников мероприятия {eve.Title}:");

                            ChatGuest(eve, callback, botClient);
                            break;


                        case "joinEvent":
                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, "Введи код мероприятия.");

                            Console.WriteLine(callback.Message.Chat.FirstName + " | Сообщение отправленно...");

                            StatusCorrector(userCall.Id, 2);
                            break;


                        case "leaveEvent":

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, "Введи код мероприятия.");

                            Console.WriteLine(callback.Message.Chat.FirstName + " | Сообщение отправленно...");

                            StatusCorrector(userCall.Id, 6);
                            break;


                        case "createEvent":
                            Console.WriteLine(callback.Message.Chat.FirstName + " | Запрос на код...");

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, "Введите код подтверждающий что вы доверенное лицо.");

                            StatusCorrector(userCall.Id, 4);
                            break;


                        case "deleteEvent":
                            Console.WriteLine(callback.Message.Chat.FirstName + " | Запрос на код...");

                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, "Введите код подтверждающий что вы доверенное лицо.");

                            StatusCorrector(userCall.Id, 7);
                            break;


                        default:
                            await botClient.SendTextMessageAsync(callback.Message.Chat.Id, "Эта кнопка не обрабатывается.");
                            break;
                    }
                    break;


                case UpdateType.MyChatMember:

                    Console.WriteLine($"Пользователь | {(update.MyChatMember.From.FirstName + ' ' + update.MyChatMember.From.LastName).Trim()}| обнулил чат...");
                    break;


                case UpdateType.Unknown:

                    Console.WriteLine($"Пользователь | ??? | совершил неизвестный update...");
                    break;


                default:

                    Console.WriteLine($"Пользователь | ??? | совершил не обрабатываемый update...");
                    break;
            }
        }

        private static Task ErrorHandler(ITelegramBotClient arg1, Exception arg2, CancellationToken arg3)
        {
            Console.WriteLine("Ошибка...\n" + arg2);
            return Task.CompletedTask;
        }

        async static void JoinEvent(Message message, ITelegramBotClient botClient)
        {
            try
            {
                using (BeerDbContext db = new BeerDbContext())
                {
                    var even = db.Events.Where(x => x.Code == message.Text).FirstOrDefault();

                    if (even != null)
                    {
                        User user = db.Users.FirstOrDefault(i => i.Id == message.From.Id);
                        var w = new Presence(user, even);

                        var v = db.Presences.Select(i => i.IdUser == user.Id && i.IdEvent == even.Id);
                        if (v.Any())
                        {
                            db.Presences.Add(w);
                            db.SaveChanges();

                            await botClient.SendTextMessageAsync(message.Chat.Id, "Вы присоединились к мероприятию.");
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Вы уже состоите в списке гостей этого мероприятия.");
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятия с таким кодом нет.");
                        Console.WriteLine(message.Chat.FirstName + " | Неверный код мероприятия...");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
            }
        }

        static void DeleteEvent(Event even)
        {
            using (BeerDbContext db = new BeerDbContext())
            {
                db.Events.Remove(even);
                db.SaveChanges();
            }
        }

        async static void LeaveEvent(Message message, ITelegramBotClient botClient)     //функция выхода из мероприятия
        {
            using (BeerDbContext db = new BeerDbContext())
            {
                var even = db.Events.Where(x => x.Code == message.Text).FirstOrDefault();

                if (even != null)
                {
                    Console.WriteLine(message.Chat.FirstName + " | Верный код мероприятия...");

                    var user = db.Users.FirstOrDefault(db => db.Id == message.From.Id);
                    var w = db.Presences.FirstOrDefault(t => t.IdUser == user.Id && t.IdEvent == even.Id);

                    if (w != null)
                    {
                        db.Presences.Remove(w);
                        db.SaveChanges();
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Вы покинули мероприятие.");
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Вы не участвовали в этом мероприятии.");
                    }
                }
                else
                {
                    Console.WriteLine(message.Chat.FirstName + " | Неверный код мероприятия...");
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Мероприятия с таким кодом нет.");
                }
            }
        }

        async static void ChatEvent(Presence i, Message message, ITelegramBotClient botClient)
        {
            using (BeerDbContext db = new BeerDbContext())
            {
                var ev = db.Events.FirstOrDefault(r => r.Id == i.IdEvent);

                InlineKeyboardMarkup button = new(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Гости", $"guestsEvents:{ev.Id}")
                    }
                });

                await botClient.SendTextMessageAsync(message.Chat.Id,
                    //$"id: {ev.Id}\n" +
                    $"Название: {ev.Title}\n" +
                    $"Дата: {ev.Start.ToShortDateString()}\n" +
                    $"Код присоединения: {ev.Code}\n" +
                    $"Статус на мероприятии: {RuRank(i.Rank)}", replyMarkup: button);
            }
        }

        static void ChangeEventRank(Event even, Message message, Rank rank)
        {
            using (BeerDbContext db = new BeerDbContext())
            {
                var user = db.Users.First(u => u.Id == message.From.Id);
                var w = db.Presences.FirstOrDefault(t => t.IdUser == user.Id && t.IdEvent == even.Id);
                w.Rank = rank;
                db.SaveChanges();
            }
        }

        async static void ChatGuest(Event even, CallbackQuery callback, ITelegramBotClient botClient)
        {
            using (BeerDbContext db = new BeerDbContext())
            {
                var x = db.Presences.Where(pres => pres.IdEvent == even.Id);
                foreach (var t in x)
                {
                    User user = db.Users.FirstOrDefault(us => us.Id == t.IdUser);
                    using (var fileStream = new FileStream(Path.GetFullPath(PathToPhoto(user.Id)), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await botClient.SendPhotoAsync(callback.Message.Chat.Id, photo: new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream),
                            $"Имя: {user.Nickname}.\n" +
                            $"Статус: {RuRank(t.Rank)}.\n" +
                            $"О себе:\n{user.AboutMe}.");
                    }
                }
            }
        }

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

        async static void PrintRandSticker(ITelegramBotClient botClient, long id)
        {
            try
            {
                string path = "stickers";
                string[] stickers;
                Random rnd = new Random();

                if (Directory.Exists(path))
                {
                    stickers = Directory.GetFiles(path, "*.web?");      //"*.webp" , "*.webm"
                    int directorySize = stickers.Length;

                    string s = stickers[rnd.Next(0, directorySize)];

                    if (s.Contains(".webm"))
                    {
                        using (var fileStream = new FileStream(Path.GetFullPath(s), FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            await botClient.SendVideoAsync(id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fileStream));
                        }
                    }
                    else if (s.Contains(".webp"))
                    {
                        using (var stick = System.IO.File.Open(s, FileMode.Open))
                        {
                            await botClient.SendStickerAsync(id, stick);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка...\n" + ex);
            }
        }

        //~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~  ~~~~~ 

        
    }
}