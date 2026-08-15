using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ePinPong.Services
{
    public class MailQueueService : BackgroundService, IMailQueueService
    {
        private readonly Channel<(string To, string Subject, string Body)> _queue =
            Channel.CreateUnbounded<(string To, string Subject, string Body)>(new UnboundedChannelOptions
            {
                SingleReader = true
            });

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MailQueueService> _logger;

        public MailQueueService(IServiceScopeFactory scopeFactory, ILogger<MailQueueService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Enqueue(string to, string subject, string body)
        {
            if (string.IsNullOrEmpty(to)) return;
            _queue.Writer.TryWrite((to, subject, body));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[MailQueueService] Pozadinski servis za slanje emailova pokrenut.");

            await foreach (var (to, subject, body) in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();
                    await mailService.SendEmailAsync(to, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MailQueueService] Greška pri pozadinskom slanju emaila na {To}", to);
                }
            }
        }
    }
}
