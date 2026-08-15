namespace ePinPong.Services
{
    public interface IMailQueueService
    {
        void Enqueue(string to, string subject, string body);
    }
}
