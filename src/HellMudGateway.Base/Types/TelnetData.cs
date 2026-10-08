namespace HellMudGateway.Base.Types;

public enum TelnetDataType
{
    Data,
    Subnegotiation,
    Will,
    Wont,
    Do,
    Dont,
}

public record TelnetData(
    TelnetDataType Type,
    byte[] Data,
    ConntectionCharset Charset,
    byte Option,
    byte[]? Raw
);