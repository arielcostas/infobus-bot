﻿using System.Net;
using BotVitrasa.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotVitrasa;

public class TelegramWorker : BackgroundService
{
    private readonly string _token;
    private readonly ILogger<TelegramWorker> _logger;
    
    private readonly BuscarCommandHandler _bch;
    private readonly DefaultCommandHandler _dch;
    private readonly InformationCommandHandler _ich;
    private readonly ParadaCommandHandler _pch;

    public TelegramWorker(IConfiguration configuration, ILogger<TelegramWorker> logger, BuscarCommandHandler bch, ParadaCommandHandler pch, InformationCommandHandler ich, DefaultCommandHandler dch)
    {
        _token = configuration["Token"] ?? string.Empty;
        _logger = logger;
        _bch = bch;
        _pch = pch;
        _ich = ich;
        _dch = dch;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        TelegramBotClient client = new(_token);
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message
            },
            ThrowPendingUpdates = true
        };

        client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken
        );

        var me = await client.GetMeAsync(cancellationToken: cancellationToken);

        Console.WriteLine($"Listening for @{me.Username}");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;

        if (message.Location is not null)
        {
            await _bch.Handle(message, botClient);
            return;
        }

        if (message.Text is not { } messageText)
            return;

        _logger.LogInformation(
            Events.MessageReceived,
            "{User}: {MessageText}",
            message.From?.Username, message.Text
        );

        var args = messageText.Split(' ');

        ICommandHandler? handler = args[0][1..] switch
        {
            "start" => _ich,
            "help" => _ich,
            "info" => _ich,
            "parada" => _pch,
            "buscar" => _bch,
            _ => null
        };

        if (handler is null)
        {
            if (int.TryParse(messageText, out _))
            {
                handler = _pch;
            }
            else
            {
                handler = _dch;
            }
        }

        await handler.Handle(message, botClient);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "API error");

        return Task.CompletedTask;
    }
}