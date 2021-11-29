using System;

namespace JLChnToZ.Katana.Expressions {
    public partial class Node: IConvertible, IFormattable {
        TypeCode IConvertible.GetTypeCode() =>
            Convert.GetTypeCode(Tag);

        bool IConvertible.ToBoolean(IFormatProvider provider) =>
            Convert.ToBoolean(Tag, provider);

        byte IConvertible.ToByte(IFormatProvider provider) =>
            Convert.ToByte(Tag, provider);

        sbyte IConvertible.ToSByte(IFormatProvider provider) =>
            Convert.ToSByte(Tag, provider);

        short IConvertible.ToInt16(IFormatProvider provider) =>
            Convert.ToInt16(Tag, provider);

        ushort IConvertible.ToUInt16(IFormatProvider provider) =>
            Convert.ToUInt16(Tag, provider);

        int IConvertible.ToInt32(IFormatProvider provider) =>
            Convert.ToInt32(Tag, provider);

        uint IConvertible.ToUInt32(IFormatProvider provider) =>
            Convert.ToUInt32(Tag, provider);

        long IConvertible.ToInt64(IFormatProvider provider) =>
            Convert.ToInt64(Tag, provider);

        ulong IConvertible.ToUInt64(IFormatProvider provider) =>
            Convert.ToUInt64(Tag, provider);

        float IConvertible.ToSingle(IFormatProvider provider) =>
            Convert.ToSingle(Tag, provider);

        double IConvertible.ToDouble(IFormatProvider provider) =>
            Convert.ToDouble(Tag, provider);

        decimal IConvertible.ToDecimal(IFormatProvider provider) =>
            Convert.ToDecimal(Tag, provider);

        char IConvertible.ToChar(IFormatProvider provider) =>
            Convert.ToChar(Tag, provider);

        DateTime IConvertible.ToDateTime(IFormatProvider provider) =>
            Convert.ToDateTime(Tag, provider);

        string IConvertible.ToString(IFormatProvider provider) =>
            Convert.ToString(Tag, provider);

        string IFormattable.ToString(string format, IFormatProvider provider) =>
            Tag is IFormattable formattable ?
            formattable.ToString(format, provider) :
            Convert.ToString(Tag, provider);

        object IConvertible.ToType(Type type, IFormatProvider provider) =>
            Convert.ChangeType(Tag, type, provider);
    }
}
