using EasyRequestHandlers.Events;
using SampleAPI.Behaviors;
using SampleAPI.Dtos;
using SampleAPI.Events.Notification;
using SampleAPI.Handlers;
using SampleAPI.Repository;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<WeatherForecastRepository>();

builder.Services.AddEasyRequestHandlers(typeof(Program))
                .WithMediatorPattern()
                .WithBehaviors(
                    typeof(LoggingBehavior<,>),
                    typeof(AuthenticationBehavior<,>),
                    typeof(ValidationBehaviour<,>)
                    )
                .WithRequestHooks()
                .Build();


builder.Services.AddEasyEventHandlers(typeof(Program));

var app = builder.Build();

app.UseHttpsRedirection();

//REQUEST HANDLERS

app.MapGet("/weather-forecast", async (CitiesForcastHandler handler, CancellationToken cancellationToken) =>
{
    return await handler.HandleAsync(cancellationToken);
});

app.MapGet("/weather-forecast/{city}", async (string city, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.SendAsync<string, WeatherForecast?>(city, cancellationToken: cancellationToken);

    return result switch
    {
        not null => Results.Ok(result),
        _ => Results.NotFound()
    };
});

app.MapPost("/weather-forecast", async (CreateCityForecastCommand command, ISender sender, CancellationToken cancellationToken) =>
{
    await sender.SendAsync<CreateCityForecastCommand, Empty>(command, cancellationToken);

    return Results.NoContent();
});


// EVENTS HANDLER

app.MapPost("/notification", async (NotificationEvent @event, IEventPublisher publisher, CancellationToken cancellationToken) =>
{
    await publisher.PublishAsync(@event, new EventPublishOptions { UseParallelExecution = true }, cancellationToken);

    return Results.NoContent();
});

app.Run();