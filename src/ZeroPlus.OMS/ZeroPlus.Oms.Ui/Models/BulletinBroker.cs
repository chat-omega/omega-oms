using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZeroPlus.Oms.Ui.Models;

public delegate void BulletinMessageHandler(BulletinMessage message);

public class BulletinBroker
{
    public event BulletinMessageHandler MessageAddedEvent;
    public event BulletinMessageHandler MessageRemovedEvent;

    private readonly object _lock = new();
    private readonly List<BulletinMessage> _messages = new();

    public List<BulletinMessage> GetAllMessages()
    {
        lock (_lock)
        {
            return _messages.ToList();
        }
    }

    public async Task AddMessageAsync(BulletinMessage message)
    {
        await Task.Run(() => AddMessage(message));
    }

    public void AddMessage(BulletinMessage message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
        MessageAddedEvent?.Invoke(message);
    }

    public void RemoveMessage(BulletinMessage message)
    {
        lock (_lock)
        {
            _messages.Remove(message);
        }
        MessageRemovedEvent?.Invoke(message);
    }
}