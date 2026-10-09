namespace HellMudGateway.Base.Types;
//Telnet数据的类型枚举
public enum TelnetDataType
{
    Data,
    Command,
    Subnegotiation,
    Will,
    Wont,
    Do,
    Dont,
}
// 代表Telnet 数据的内部类型
// 将接收到的Telnet原始数据，分解为 指令，选项，子协商和用回车分割的数据，进行内部处理
public record TelnetData(
    // Telnet数据的类型
    TelnetDataType Type,
    // 数据的负载
    byte[] Data,
    // 编码
    TelnetCharset Charset,
    // 选项值，选项和子协商类型的数据会使用
    byte Option,
    // 原始数据，Raw不为空则直接写入，避免对原始数据的再次处理
    byte[]? Raw
);