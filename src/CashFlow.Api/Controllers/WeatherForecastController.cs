using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;

namespace CashFlow.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private readonly IConnection _rabbitMqConnection;

    public WeatherForecastController([FromKeyedServices("rabbitmq")] IConnection rabbitMqConnection)
    {
        _rabbitMqConnection = rabbitMqConnection;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        // Log RabbitMQ connection status
        if (_rabbitMqConnection.IsOpen)
        {
            // Could publish messages to RabbitMQ here if needed
        }

        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}

