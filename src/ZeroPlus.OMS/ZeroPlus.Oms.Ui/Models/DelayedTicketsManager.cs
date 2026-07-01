using System;
using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public class DelayedTicketsManager
    {
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<Tuple<string, Side>, ComplexOrderTicketViewModel> _delayedTicketKeyToTicketMap = new();

        internal bool SetTicketIfNotExists(ComplexOrderTicketViewModel ticket)
        {
            Tuple<string, Side> key = Tuple.Create(ticket.SpreadId, ticket.SubmitWithDelaySide);
            bool found = false;
            lock (_lock)
            {
                if (!_delayedTicketKeyToTicketMap.TryGetValue(key, out ComplexOrderTicketViewModel otherTicket))
                {
                    _delayedTicketKeyToTicketMap[key] = ticket;
                }
                else
                {
                    found = otherTicket != ticket;
                }
                return found;
            }
        }
    }
}
