using System.Buffers;
using System.Net.Sockets;
using System.Threading.Channels;
using HellMudGateway.Base.Types;
using HellMudGateway.Client.Helpers;

namespace HellMudGateway.Client.Infras.Components;

public sealed class TelnetDecoder
{
    public const int DefaultBufferSize = 256;
    public const int StatusNormal = 0;
    public const int StatusIAC = 1;
    public const int StatusOption = 2;
    public const int StatusSb = 3;
    public const int StatusSbIac = 4;
    public TelnetCharset Charset { get; set; }

    private byte[] Buffer;
    private int Count;
    private int Status;
    public TelnetDecoder(TelnetCharset charset)
    {
        Charset = charset;
        Buffer = new byte[DefaultBufferSize];
        Count = 0;
        Status = StatusNormal;
    }
    public void Reset()
    {
        Count = 0;
        Status = StatusNormal;
    }
    private void Add(byte data)
    {
        if (Count + 1 > Buffer.Length)
        {
            var newBuffer = new byte[Buffer.Length * 2];
            Array.Copy(Buffer, newBuffer, Count);
            Buffer = newBuffer;
        }
        Buffer[Count] = data;
        Count++;
    }
    private byte[] Flush()
    {
        var result = new byte[Count];
        Array.Copy(Buffer, result, Count);
        Count = 0;
        return result;
    }
    private void Clear()
    {
        Count = 0;
    }
    public TelnetData[] OnByte(byte data)
    {
        Add(data);
        switch (Status)
        {
            case StatusNormal:
                switch (data)
                {
                    case TelnetDataHelper.IAC:
                        Status = StatusIAC;
                        break;
                    case TelnetDataHelper.LF:
                        var rawdata = Flush();
                        return [TelnetDataHelper.CreateTelnetData(rawdata, TelnetDataHelper.UnescapeIAC(rawdata), Charset)];
                }
                return Array.Empty<TelnetData>();
            case StatusIAC:
                if (data == TelnetDataHelper.IAC)
                {
                    Status = StatusNormal;
                }
                else
                {
                    TelnetData? beforeResult = null;
                    var beforeIACData = Flush();
                    if (beforeIACData.Length > 2)
                    {
                        beforeResult = TelnetDataHelper.CreateTelnetData(beforeIACData, TelnetDataHelper.UnescapeIAC(beforeIACData), Charset);
                    }
                    switch (data)
                    {
                        case TelnetDataHelper.WILL:
                        case TelnetDataHelper.WONT:
                        case TelnetDataHelper.DO:
                        case TelnetDataHelper.DONT:
                            Status = StatusOption;
                            if (beforeResult is not null)
                            {
                                return [beforeResult];
                            }
                            break;
                        case TelnetDataHelper.SB:
                            Status = StatusSb;
                            if (beforeResult is not null)
                            {
                                return [beforeResult];
                            }
                            break;
                        default:
                            Clear();
                            var cmddata = TelnetDataHelper.CreateTelnetCommand([TelnetDataHelper.IAC, data], data);
                            if (beforeResult is not null)
                            {
                                return [beforeResult, cmddata];
                            }
                            return [cmddata];
                    }
                }
                break;
            case StatusOption:
                Status = StatusNormal;
                var optionData = Flush();
                return new[] { TelnetDataHelper.CreateTelnetOption(optionData, optionData[optionData.Length - 2], data) };
            case StatusSb:
                if (data == TelnetDataHelper.IAC)
                {
                    Status = StatusSbIac;
                }
                return Array.Empty<TelnetData>();
            case StatusSbIac:
                if (data == TelnetDataHelper.IAC)
                {
                    Status = StatusSb;
                }
                else if (data == TelnetDataHelper.SE)
                {
                    Status = StatusNormal;
                    var sbdata = Flush();
                    if (sbdata.Length < 5)
                    {
                        throw new InvalidOperationException("Invalid subnegotiation data.");
                    }
                    return [TelnetDataHelper.CreateSubnegotiation(sbdata, sbdata[2], sbdata[2..(sbdata.Length - 2)], TelnetDataHelper.TextSubnegotiationMap.ContainsKey(sbdata[2]) ? Charset : TelnetCharset.Binary)];
                }
                else
                {
                    Clear();
                    Status = StatusNormal;
                }
                break;
        }
        return [];
    }
    public async Task ReadFromConnectionAsync(IDataStreamHolder holder, Channel<TelnetData> channel, CancellationTokenSource _cancellation)
    {
        var buffer = new byte[8192];
        Exception? error = null;

        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var bytesRead = await holder.DataStream.ReadAsync(buffer, _cancellation.Token);
                if (bytesRead == 0)
                {
                    break;
                }
                for (int i = 0; i < bytesRead; i++)
                {
                    var data = buffer[i];
                    var decoded = OnByte(data);
                    foreach (var telnetData in decoded)
                    {
                        await channel.Writer.WriteAsync(telnetData, _cancellation.Token);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (IOException exception)
        {
            error = exception;
        }
        catch (SocketException exception)
        {
            error = exception;
        }
        catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            channel.Writer.TryComplete(error);
        }
    }
    public async Task<bool> WriteToConnectionAsync(IDataStreamHolder holder, Channel<TelnetData> channel, CancellationTokenSource _cancellation)
    {
        try
        {
            await foreach (var data in channel.Reader.ReadAllAsync(_cancellation.Token))
            {
                await TelnetDataHelper.WriteToStream(data, holder.DataStream);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            return !_cancellation.IsCancellationRequested;

        }
        return false;
    }

}