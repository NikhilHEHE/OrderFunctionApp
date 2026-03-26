using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFunctionApp.Data;
using System;
using OrderFunctionApp.Models;

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
            }
            else
            {
                _logger.LogWarning($"Order {orderId} not found");
            }
        }
    }
}