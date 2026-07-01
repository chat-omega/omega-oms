namespace ZeroPlus.Oms.Ui.Models;

public class Notifier
{
    public readonly string Name;
    public volatile bool IsUpdated;

    public Notifier(string name)
    {
        Name = name;
    }

    public void Updated()
    {
        IsUpdated = true;
    }
}