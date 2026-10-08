using System.Threading.Channels;

namespace HellMudGateway.Base.Types;

//Telnet的连接类型
public enum RawConnectionAdressType
{
    V4,
    V6,
}
//原生的telnet连接接口
public interface IRawConnection
{
    public string RemoteAddress { get; }
    public int RemotePort { get; }
    public ConnectionPort Local { get; }
    public void Close();
    public RawConnectionAdressType AddressType { get; }
    Channel<TelnetData> Input { get; }
    Channel<TelnetData> Output { get; }
}
