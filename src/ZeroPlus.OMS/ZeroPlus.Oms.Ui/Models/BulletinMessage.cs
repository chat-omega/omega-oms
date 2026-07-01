using System;

namespace ZeroPlus.Oms.Ui.Models;

public class BulletinMessage
{
    public DateTime Time { get; set; }
    public string Message { get; set; }
    public string Source { get; set; }
}