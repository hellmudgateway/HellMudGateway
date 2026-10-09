using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using HellMudGateway.Base.Types;
using HellMudGateway.Client.Helpers;

namespace HellMudGateway.Client.Infras.Components;

public sealed class ClientConnection : IRawConnection,IDataStreamHolder
{
    private readonly bool CompressDisabled;
    private readonly TcpClient _telnetConnection;
    public Stream DataStream{get;set;}
    private readonly CancellationTokenSource _cancellation = new();
    private int _closed;

    public ClientConnection(TcpClient telnetConnection, ConnectionPort local, bool compressDisabled)
    {
        ArgumentNullException.ThrowIfNull(telnetConnection);

        _telnetConnection = telnetConnection;
        CompressDisabled = compressDisabled;
        if (!telnetConnection.Connected ||
            telnetConnection.Client.RemoteEndPoint is not IPEndPoint remoteEndPoint ||
            telnetConnection.Client.LocalEndPoint is not IPEndPoint localEndPoint)
        {
            throw new ArgumentException("The Telnet TCP connection must be connected.", nameof(telnetConnection));
        }

        DataStream = telnetConnection.GetStream();
        RemoteAddress = remoteEndPoint.Address.ToString();
        RemotePort = remoteEndPoint.Port;
        Local = local;

        AddressType = remoteEndPoint.Address.AddressFamily == AddressFamily.InterNetworkV6
            ? RawConnectionAdressType.V6
            : RawConnectionAdressType.V4;

        Input = Channel.CreateUnbounded<TelnetData>();
        Output = Channel.CreateUnbounded<TelnetData>();
        Decoder = new(Local.Charset);
        _ = ReadFromConnectionAsync();
        _ = WriteToConnectionAsync();
    }

    public string RemoteAddress { get; }
    public int RemotePort { get; }
    public ConnectionPort Local { get; }
    public RawConnectionAdressType AddressType { get; }
    public Channel<TelnetData> Input { get; }
    public Channel<TelnetData> Output { get; }
    private TelnetDecoder Decoder { get; set; }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        Input.Writer.TryComplete();
        Output.Writer.TryComplete();
        _telnetConnection.Dispose();
    }

    private async Task ReadFromConnectionAsync()
    {
        await Decoder.ReadFromConnectionAsync(this, Input, _cancellation);
    }

    private async Task WriteToConnectionAsync()
    {
        if (await Decoder.WriteToConnectionAsync(this, Output, _cancellation))
        {
            Close();
            Output.Reader.Completion.ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}
