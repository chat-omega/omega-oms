using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Trading;

public class RouteToBrokerExchangeLookup : IReadOnlyDictionary<string, (Broker broker, Exchange exchange)>
{
    private readonly ConcurrentDictionary<string, (Broker broker, Exchange exchange)> _map = new();

    public int Count => _map.Count;
    public IEnumerable<string> Keys => _map.Keys;
    public IEnumerable<(Broker broker, Exchange exchange)> Values => _map.Values;

    public RouteToBrokerExchangeLookup()
    {
        _map["DAMEX"] = (Broker.DASH, Exchange.AMEX);
        _map["DARCA"] = (Broker.DASH, Exchange.ARCA);
        _map["DBATS"] = (Broker.DASH, Exchange.BATS);
        _map["DBOX"] = (Broker.DASH, Exchange.BOX);
        _map["DC2"] = (Broker.DASH, Exchange.C2);
        _map["DCBOE"] = (Broker.DASH, Exchange.CBOE);
        _map["DEDGX"] = (Broker.DASH, Exchange.EDGX);
        _map["DEMLD"] = (Broker.DASH, Exchange.EMLD);
        _map["DGMNI"] = (Broker.DASH, Exchange.GMNI);
        _map["DISE"] = (Broker.DASH, Exchange.ISE);
        _map["DMCRY"] = (Broker.DASH, Exchange.MCRY);
        _map["DMEMX"] = (Broker.DASH, Exchange.MEMX);
        _map["DMIAX"] = (Broker.DASH, Exchange.MIAX);
        _map["DNASDAQ"] = (Broker.DASH, Exchange.NASDAQ);
        _map["DNQBX"] = (Broker.DASH, Exchange.NQBX);
        _map["DPEARL"] = (Broker.DASH, Exchange.PEARL);
        _map["DPHLX"] = (Broker.DASH, Exchange.PHLX);
        _map["DSPHR"] = (Broker.DASH, Exchange.SPHR);
        _map["DSENSOR"] = (Broker.DASH, Exchange.SMART);
        _map["DSMOKE"] = (Broker.DASH, Exchange.SMART);
        _map["DSTRIKE"] = (Broker.DASH, Exchange.SMART);

        _map["BATS"] = (Broker.IB, Exchange.BATS);
        _map["BOX"] = (Broker.IB, Exchange.BOX);
        _map["BX"] = (Broker.IB, Exchange.BOX);
        _map["C2"] = (Broker.IB, Exchange.C2);
        _map["CBOE"] = (Broker.IB, Exchange.CBOE);
        _map["GNMI"] = (Broker.IB, Exchange.GMNI);
        _map["ISE"] = (Broker.IB, Exchange.ISE);
        _map["MIAX"] = (Broker.IB, Exchange.MIAX);
        _map["NASDAQ"] = (Broker.IB, Exchange.NASDAQ);
        _map["NYSEARCA"] = (Broker.IB, Exchange.ARCA);
        _map["PHLX"] = (Broker.IB, Exchange.PHLX);
        _map["IB"] = (Broker.IB, Exchange.SMART);
        _map["SMART"] = (Broker.IB, Exchange.SMART);

        _map["IAMEX"] = (Broker.INET, Exchange.AMEX);
        _map["IARCA"] = (Broker.INET, Exchange.ARCA);
        _map["IBATS"] = (Broker.INET, Exchange.BATS);
        _map["IBOX"] = (Broker.INET, Exchange.BOX);
        _map["IC2"] = (Broker.INET, Exchange.C2);
        _map["ICBOE"] = (Broker.INET, Exchange.CBOE);
        _map["IEDGX"] = (Broker.INET, Exchange.EDGX);
        _map["IEMLD"] = (Broker.INET, Exchange.EMLD);
        _map["IGMNI"] = (Broker.INET, Exchange.GMNI);
        _map["IISE"] = (Broker.INET, Exchange.ISE);
        _map["IMCRY"] = (Broker.INET, Exchange.MCRY);
        _map["IMEMX"] = (Broker.INET, Exchange.MEMX);
        _map["IMIAX"] = (Broker.INET, Exchange.MIAX);
        _map["INASDAQ"] = (Broker.INET, Exchange.NASDAQ);
        _map["INQBX"] = (Broker.INET, Exchange.NQBX);
        _map["IPEARL"] = (Broker.INET, Exchange.PEARL);
        _map["IPHLX"] = (Broker.INET, Exchange.PHLX);
        _map["ISPHR"] = (Broker.INET, Exchange.SPHR);
        _map["ISMART"] = (Broker.INET, Exchange.SMART);
        _map["ISPREAD"] = (Broker.INET, Exchange.SMART);
        _map["SMARTOPT"] = (Broker.INET, Exchange.SMART);
        _map["SMARTOPTS"] = (Broker.INET, Exchange.SMART);

        _map["MARCA"] = (Broker.MTRX, Exchange.ARCA);
        _map["MAMEX"] = (Broker.MTRX, Exchange.AMEX);
        _map["MBATS"] = (Broker.MTRX, Exchange.BATS);
        _map["MBOX"] = (Broker.MTRX, Exchange.BOX);
        _map["MC2"] = (Broker.MTRX, Exchange.C2);
        _map["MCBOE"] = (Broker.MTRX, Exchange.CBOE);
        _map["MEDGX"] = (Broker.MTRX, Exchange.EDGX);
        _map["MEMLD"] = (Broker.MTRX, Exchange.EMLD);
        _map["MGMNI"] = (Broker.MTRX, Exchange.GMNI);
        _map["MISE"] = (Broker.MTRX, Exchange.ISE);
        _map["MMCRY"] = (Broker.MTRX, Exchange.MCRY);
        _map["MMEX"] = (Broker.MTRX, Exchange.MEMX);
        _map["MMIAX"] = (Broker.MTRX, Exchange.MIAX);
        _map["MNASDAQ"] = (Broker.MTRX, Exchange.NASDAQ);
        _map["MNQBX"] = (Broker.MTRX, Exchange.NQBX);
        _map["MPEARL"] = (Broker.MTRX, Exchange.PEARL);
        _map["MPHLX"] = (Broker.MTRX, Exchange.PHLX);
        _map["MSPHR"] = (Broker.MTRX, Exchange.SPHR);
        _map["MSMART"] = (Broker.MTRX, Exchange.SMART);
        _map["MHUNTER"] = (Broker.MTRX, Exchange.SMART);
        _map["MSYNTHETIC"] = (Broker.MTRX, Exchange.SMART);

        _map["BAMEX"] = (Broker.RQD, Exchange.AMEX);
        _map["BARCA"] = (Broker.RQD, Exchange.ARCA);
        _map["BBATS"] = (Broker.RQD, Exchange.BATS);
        _map["BBOX"] = (Broker.RQD, Exchange.BOX);
        _map["BC2"] = (Broker.RQD, Exchange.C2);
        _map["BCBOE"] = (Broker.RQD, Exchange.CBOE);
        _map["BEDGX"] = (Broker.RQD, Exchange.EDGX);
        _map["BEMLD"] = (Broker.RQD, Exchange.EMLD);
        _map["BGMNI"] = (Broker.RQD, Exchange.GMNI);
        _map["BISE"] = (Broker.RQD, Exchange.ISE);
        _map["BMCRY"] = (Broker.RQD, Exchange.MCRY);
        _map["BMEMX"] = (Broker.RQD, Exchange.MEMX);
        _map["BMIAX"] = (Broker.RQD, Exchange.MIAX);
        _map["BNASDAQ"] = (Broker.RQD, Exchange.NASDAQ);
        _map["BNQBX"] = (Broker.RQD, Exchange.NQBX);
        _map["BPEARL"] = (Broker.RQD, Exchange.PEARL);
        _map["BPHLX"] = (Broker.RQD, Exchange.PHLX);
        _map["BSPHR"] = (Broker.RQD, Exchange.SPHR);
        _map["BMMFREE"] = (Broker.RQD, Exchange.SMART);
        _map["BMMSWEEP"] = (Broker.RQD, Exchange.SMART);
        _map["BTBBLAST"] = (Broker.RQD, Exchange.SMART);
        _map["EXCH_ROLL"] = (Broker.RQD, Exchange.SMART);
        _map["EXCH_ROLL_S"] = (Broker.RQD, Exchange.SMART);
        _map["EXCH_ROLL_SR"] = (Broker.RQD, Exchange.SMART);
        _map["TBCXSPRAY"] = (Broker.RQD, Exchange.SMART);
        _map["ZPROLL"] = (Broker.RQD, Exchange.SMART);
        _map["ZPSROLL"] = (Broker.RQD, Exchange.SMART);
        _map["ARCO"] = (Broker.RQD, Exchange.ARCA);
        _map["XBOX"] = (Broker.RQD, Exchange.BOX);
        _map["C2OX"] = (Broker.RQD, Exchange.C2);
        _map["XCBO"] = (Broker.RQD, Exchange.CBOE);
        _map["EDGO"] = (Broker.RQD, Exchange.EDGX);
        _map["EMLD"] = (Broker.RQD, Exchange.EMLD);
        _map["XISX"] = (Broker.RQD, Exchange.ISE);
        _map["MCRY"] = (Broker.RQD, Exchange.MCRY);
        _map["XMIO"] = (Broker.RQD, Exchange.MIAX);
        _map["XNDQ"] = (Broker.RQD, Exchange.NASDAQ);
        _map["XPHO"] = (Broker.RQD, Exchange.PHLX);
        _map["AMXO"] = (Broker.RQD, Exchange.AMEX);
        _map["BATO"] = (Broker.RQD, Exchange.BATS);
        _map["XBXO"] = (Broker.RQD, Exchange.NQBX);
        _map["GMNI"] = (Broker.RQD, Exchange.GMNI);
        _map["MPRL"] = (Broker.RQD, Exchange.PEARL);
    }

    public (Broker broker, Exchange exchange) this[string key] => _map[key];

    public IEnumerator<KeyValuePair<string, (Broker broker, Exchange exchange)>> GetEnumerator()
    {
        return _map.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool ContainsKey(string key)
    {
        return _map.ContainsKey(key);
    }

    public bool TryGetValue(string key, out (Broker broker, Exchange exchange) value)
    {
        return _map.TryGetValue(key, out value);
    }
}