namespace HellMudGateway.Base.Types;

public class ConnectionPort(string address, int port, TelnetCharset charset)
{
    public string Address { get; } = address;
    public int Port { get; } = port;
    public TelnetCharset Charset { get; } = charset;
}