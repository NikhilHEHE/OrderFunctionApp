using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFunctionApp.Data;
using System;
using OrderFunctionApp.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace OrderFunctionApp
{
    public class ProcessOrderFunction
    {
        private readonly ILogger _logger;

        public ProcessOrderFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessOrderFunction>();
        }

        [Function("ProcessOrder")]
        public async Task Run(
            [QueueTrigger("order-queue")] string message)
        {
            _logger.LogInformation($"Raw message: {message}");

            // Extract OrderId
            var orderId = int.Parse(message.Replace("OrderId:", ""));

            // Get connection string
            var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var context = new AppDbContext(options);

            var order = await context.Orders.FindAsync(orderId);

            if (order != null)
            {
                order.Status = OrderStatus.Completed;
                await context.SaveChangesAsync();

                _logger.LogInformation($"Order {orderId} marked as Completed");

                var logicAppUrl = "https://prod-02.centralindia.logic.azure.com:443/workflows/5c34a2145a504a9d9fd6d46293b1a65e/triggers/When_an_HTTP_request_is_received/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2FWhen_an_HTTP_request_is_received%2Frun&sv=1.0&sig=hOW8G0aXT-4721H2Qt27ue7fTa-UL7mRg4oeHaR3oEo";

                var payload = new
                {
                    orderId = order.Id,
                    email = order.CustomerEmail,
                    customerName = order.CustomerName
                };

                var json = JsonSerializer.Serialize(payload);

                using var httpClient = new HttpClient();
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await httpClient.PostAsync(logicAppUrl, content);

                _logger.LogInformation($"Logic App triggered for Order {orderId}");
            }
            else
            {
                _logger.LogWarning($"Order {orderId} not found");
            }
        }
    }
}