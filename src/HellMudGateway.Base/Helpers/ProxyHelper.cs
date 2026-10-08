using HellMudGateway.Base.Types;

namespace HellMudGateway.Base.Helpers;

public class ProxyHelper
{
    public static string BuildHAProxyHeader(IRawConnection connection, string Host, int Port)
    {
        return $"PROXY {(connection.AddressType == RawConnectionAdressType.V4 ? "TCP4" : "TCP6")} {connection.RemoteAddress} {Host} {connection.RemotePort} {Port}\r\n";
    }
}