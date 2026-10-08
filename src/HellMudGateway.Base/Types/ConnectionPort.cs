namespace HellMudGateway.Base.Types;

public class ConnectionPort(string address, int port, ConntectionCharset charset)
{
    public string Address { get; } = address;
    public int Port { get; } = port;
    public ConntectionCharset Charset { get; } = charset;
}