namespace GamingCafe.ClientAgent.Messaging;

public interface IMessageHandler
{
    void RegisterHandler<T>(string messageType, Func<T, Task> handler) where T : BaseMessage;
    Task ProcessInboundMessageAsync<T>(T message) where T : BaseMessage;
    bool HasProcessed(string messageId);
}
