using System.Text;
using HellMudGateway.Base.Types;

namespace HellMudGateway.Client.Helpers;

//用来对TelnetData进行处理的帮助类
public static class TelnetDataHelper
{
    //telnet IAC 指令
    public const byte IAC = 255;
    //telnet IAC 指令: DONT, DO, WONT, WILL, SB, SE
    public const byte DONT = 254;
    public const byte DO = 253;
    public const byte WONT = 252;
    public const byte WILL = 251;
    //telnet IAC 指令: SB, SE
    public const byte SB = 250;
    public const byte SE = 240;
    public static void Init()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    // 对IAC字节进行转义
    public static byte[] EscapeIAC(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        using var ms = new MemoryStream();
        foreach (var b in raw)
        {
            ms.WriteByte(b);
            if (b == IAC)
            {
                ms.WriteByte(IAC);
            }
        }
        return ms.ToArray();
    }
    //将telnet转换为byte[]并写入Stream
    public static void WriteToStream(TelnetData data, Stream stream)
    {
        switch (data.Type)
        {
            case TelnetDataType.Will:
                stream.Write([IAC, WILL, data.Option], 0, 3);
                break;
            case TelnetDataType.Wont:
                stream.Write([IAC, WONT, data.Option], 0, 3);
                break;
            case TelnetDataType.Do:
                stream.Write([IAC, DO, data.Option], 0, 3);
                break;
            case TelnetDataType.Dont:
                stream.Write([IAC, DONT, data.Option], 0, 3);
                break;
            case TelnetDataType.Data:
                var escapedData = EscapeIAC(data.Data);
                stream.Write(escapedData, 0, escapedData.Length);
                break;
            case TelnetDataType.Subnegotiation:
                var escapedSubnegotiation = EscapeIAC(data.Data);
                stream.Write([IAC, SB, data.Option], 0, 3);
                stream.Write(escapedSubnegotiation, 0, escapedSubnegotiation.Length);
                stream.Write([IAC, SE], 0, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    //将 TelnetData 以指定的Charset写入Stream
    public static void WriteData(TelnetData data, Stream stream, ConntectionCharset charset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(stream);
        if (charset == ConntectionCharset.Binary)
        {
            throw new ArgumentException("Charset cannot be binary.", nameof(charset));
        }
        if (data.Charset != ConntectionCharset.Binary && charset != data.Charset)
        {
            data = ConvertToCharset(data, charset);
        }
        if (data.Raw != null)
        {
            stream.Write(data.Raw, 0, data.Raw.Length);
        }
        else
        {
            WriteToStream(data, stream);
        }
    }
    //GB18030 Encoding
    private static Encoding GB18030Encoding => Encoding.GetEncoding("GB18030");
    //Big5 Encoding
    private static Encoding BIG5Encoding => Encoding.GetEncoding(950);
    //转换Telnet到知道的Charset
    private static TelnetData ConvertToCharset(TelnetData data, ConntectionCharset charset)
    {
        string decoded = data.Charset switch
        {
            ConntectionCharset.UTF8 => Encoding.UTF8.GetString(data.Data),
            ConntectionCharset.GB18030 => GB18030Encoding.GetString(data.Data),
            ConntectionCharset.BIG5 => BIG5Encoding.GetString(data.Data),
            _ => throw new ArgumentOutOfRangeException(),
        };
        return charset switch
        {
            ConntectionCharset.UTF8 => data with { Data = Encoding.UTF8.GetBytes(decoded), Charset = charset, Raw = null },
            ConntectionCharset.GB18030 => data with { Data = GB18030Encoding.GetBytes(decoded), Charset = charset, Raw = null },
            ConntectionCharset.BIG5 => data with { Data = BIG5Encoding.GetBytes(decoded), Charset = charset, Raw = null },
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}