using botTelegram.DateBase;
using botTelegram.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;
using Telegram.Bot.Types;
using User = botTelegram.Models.User;
using botTelegram.ExtensionMethods;
using static System.Net.Mime.MediaTypeNames;

namespace botTelegram.UpdateHandler;

public class UpdateHendler
{


    public async Task UpdateHandlerAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
    {
        string password = "tomatoday";

        var handler = update.Type switch
        {
            UpdateType.Message => BotOnMessageReceived(update.Message, botClient),


            _ => Task.CompletedTask
        };

        await handler;
    }


    async Task BotOnMessageReceived(Message message, ITelegramBotClient botClient)
    {
        Console.WriteLine(message.Chat.FirstName + " " + message.Chat.LastName + "   |   " + message.Text);

        if (!ExtensionMethods.ExtensionMethods.RegistrationCheck(botClient, message))
            return;

        var messageHandler = message.Type switch
        {
            MessageType.Text => TextHandler(message, botClient),


            _ => Task.CompletedTask
        };
        await messageHandler ;
    }

    async Task TextHandler(Message message, ITelegramBotClient botClient)
    {
        if (message.Text[0] == '/')
        {
            var comandHendler = message.Text switch
            {
                "/help" => HelpTextCommand(),

                _ => botClient.SendTextMessageAsync(message.Chat.Id, "Такого не знаем(.")
            };


            switch (message.Text)
            {
                case "/help":

                    Console.WriteLine(message.Chat.FirstName + " | Пользователь узнаёт команды...");

                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вот список команд:\n" +
                        "| /help - Показывает все нужные команды.\n" +
                        "| /me - Показывает информацию о вас.\n" +
                        "| /events - Взаимодействие с мероприятиями.\n" +
                        "| /back - Прекращает текущее действие.");

                    using (BeerDbContext db = new BeerDbContext())
                        wwwwwuserInfo.SetStateAndSave(db, 0);
                    break;


                case "/me":

                    Console.WriteLine(message.Chat.FirstName + " | Пользователь решил узнать информацию о себе...");

                    InlineKeyboardMarkup button1 = new(new[]
                    {
                            new[]
                                    {
                                InlineKeyboardButton.WithCallbackData("Изменить имя", $"changeNickname:{userInfo.Id}")
                            },
                            new[]
                                    {
                                InlineKeyboardButton.WithCallbackData("Изменить о себе", $"changeAboutMe:{userInfo.Id}")
                            },
                            new[]
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
                                new[]
                                        {
                                    InlineKeyboardButton.WithCallbackData("Присоединиться", $"joinEvent:{userInfo.Id}")
                                },
                                new[]
                                        {
                                    InlineKeyboardButton.WithCallbackData("Покинуть", $"leaveEvent:{userInfo.Id}")
                                },
                                new[]
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

    async Task HelpTextCommand()
    {

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
