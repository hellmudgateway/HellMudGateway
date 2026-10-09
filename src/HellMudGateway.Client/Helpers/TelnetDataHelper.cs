using System.Runtime.InteropServices;
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
    public const byte LF=0x0a;
    public static Dictionary<byte, bool> TextSubnegotiationMap { get; } = new();
    //将特定option的子协商注册为文本模式
    //注册为文本模式的子协商在创建时会被视为文本数据，将进行转码
    public static void RegisterTextModeSubnegotiation(byte option)
    {
        TextSubnegotiationMap[option] = true;
    }
    //初始化
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
    // 对转义的IAC字节进行还原
    public static byte[] UnescapeIAC(byte[] escaped)
    {
        ArgumentNullException.ThrowIfNull(escaped);
        using var ms = new MemoryStream();
        for (int i = 0; i < escaped.Length; i++)
        {
            var b = escaped[i];
            ms.WriteByte(b);
            if (b == IAC && i + 1 < escaped.Length && escaped[i + 1] == IAC)
            {
                i++; // skip the next IAC
            }
        }
        return ms.ToArray();
    }
    //将telnet转换为byte[]并写入Stream
    public static async Task WriteToStream(TelnetData data, Stream stream)
    {
        switch (data.Type)
        {
            case TelnetDataType.Will:
                await stream.WriteAsync([IAC, WILL, data.Option], 0, 3);
                break;
            case TelnetDataType.Wont:
                await stream.WriteAsync([IAC, WONT, data.Option], 0, 3);
                break;
            case TelnetDataType.Do:
                await stream.WriteAsync([IAC, DO, data.Option], 0, 3);
                break;
            case TelnetDataType.Dont:
                await stream.WriteAsync([IAC, DONT, data.Option], 0, 3);
                break;
            case TelnetDataType.Data:
                var escapedData = EscapeIAC(data.Data);
                await stream.WriteAsync(escapedData, 0, escapedData.Length);
                break;
            case TelnetDataType.Subnegotiation:
                var escapedSubnegotiation = EscapeIAC(data.Data);
                await stream.WriteAsync([IAC, SB, data.Option], 0, 3);
                await stream.WriteAsync(escapedSubnegotiation, 0, escapedSubnegotiation.Length);
                await stream.WriteAsync([IAC, SE], 0, 2);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    //将 TelnetData 以指定的Charset写入Stream
    public static async Task WriteData(TelnetData data, Stream stream, TelnetCharset charset)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(stream);
        if (charset == TelnetCharset.Binary)
        {
            throw new ArgumentException("Charset cannot be binary.", nameof(charset));
        }
        if (data.Charset != TelnetCharset.Binary && charset != data.Charset)
        {
            data = ConvertToCharset(data, charset);
        }
        if (data.Raw != null)
        {
            await stream.WriteAsync(data.Raw.AsMemory(0, data.Raw.Length));
        }
        else
        {
            await WriteToStream(data, stream);
        }
    }
    //GB18030 Encoding
    private static Encoding GB18030Encoding => Encoding.GetEncoding("GB18030");
    //Big5 Encoding
    private static Encoding BIG5Encoding => Encoding.GetEncoding(950);
    //转换Telnet到知道的Charset
    private static TelnetData ConvertToCharset(TelnetData data, TelnetCharset charset)
    {
        string decoded = data.Charset switch
        {
            TelnetCharset.UTF8 => Encoding.UTF8.GetString(data.Data),
            TelnetCharset.GB18030 => GB18030Encoding.GetString(data.Data),
            TelnetCharset.BIG5 => BIG5Encoding.GetString(data.Data),
            _ => throw new ArgumentOutOfRangeException(),
        };
        return charset switch
        {
            TelnetCharset.UTF8 => data with { Data = Encoding.UTF8.GetBytes(decoded), Charset = charset, Raw = null },
            TelnetCharset.GB18030 => data with { Data = GB18030Encoding.GetBytes(decoded), Charset = charset, Raw = null },
            TelnetCharset.BIG5 => data with { Data = BIG5Encoding.GetBytes(decoded), Charset = charset, Raw = null },
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
    //创建数据型的 TelnetData，raw代表原始数据，data为未转码过的数据正文
    public static TelnetData CreateTelnetData(byte[]? raw, byte[] data, TelnetCharset charset)
    {
        return new TelnetData
        (
            TelnetDataType.Data,
            data,
             charset,
             0,
             raw
        );
    }
    //创建命令型的 TelnetData，raw代表原始数据，cmd为命令字
    public static TelnetData CreateTelnetCommand(byte[]? raw, byte cmd)
    {
        return new TelnetData
        (
            TelnetDataType.Command,
            Array.Empty<byte>(),
             TelnetCharset.Binary,
             cmd,
             raw
        );
    }
    //创建选项型的 TelnetData，raw代表原始数据，type为选项类型，option为选项字
    public static TelnetData CreateTelnetOption(byte[]? raw, byte type, byte option)
    {
        var ot = type switch
        {
            WILL => TelnetDataType.Will,
            WONT => TelnetDataType.Wont,
            DO => TelnetDataType.Do,
            _ => TelnetDataType.Dont,
        };
        return new TelnetData
        (
            ot,
            Array.Empty<byte>(),
            TelnetCharset.Binary,
            option,
            raw
        );
    }
    //创建子协商型的 TelnetData，raw代表原始数据，option为子协商选项字，data为子协商数据正文(未转码)
    public static TelnetData CreateSubnegotiation(byte[]? raw, byte option, byte[] data, TelnetCharset charset)
    {
        return new TelnetData
        (
            TelnetDataType.Subnegotiation,
            data,
             charset,
             option,
             raw
        );
    }

}