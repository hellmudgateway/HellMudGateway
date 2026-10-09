namespace HellMudGateway.Base.Types;

//连接的字符集类型
public enum TelnetCharset
{
    //2进制数据,不进行转换
    Binary = 0,
    //UTF-8编码文本
    UTF8 = 1,
    //GB18030编码文本
    GB18030 = 2,
    //BIG5编码文本
    BIG5 = 3
}