namespace HellMudGateway.Base.Types;

//网关命令类型
public record GatewayCommand(
    //指令编号
    int Command,
    //数据
    ReadOnlyMemory<byte> Data,
    //数据编码
    ConntectionCharset Charset
);