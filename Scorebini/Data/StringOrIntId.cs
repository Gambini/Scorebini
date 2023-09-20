using System;
using Newtonsoft.Json;

namespace Scorebini.Data
{

    [JsonConverter(typeof(StringOrIntIdConverter))]
    public struct StringOrIntId : IComparable<StringOrIntId>, IEquatable<StringOrIntId>
    {
        public enum IdType
        {
            String,
            Int
        }

        public IdType Type { get; }
        public string StringVal { get; }
        public long IntVal { get; }

        public bool HasValue => Type != IdType.String || StringVal != null;

        public StringOrIntId()
        {
            Type = IdType.String;
            StringVal = null;
            IntVal = default;
        }

        public StringOrIntId(string idStr)
        {
            Type = IdType.String;
            StringVal = idStr;
            IntVal = default;
        }

        public StringOrIntId(long idInt)
        {
            Type = IdType.Int;
            StringVal = default;
            IntVal = idInt;
        }

        public int CompareTo(StringOrIntId other)
        {
            if (this.Type == IdType.String)
            {
                if (this.StringVal == null && other.StringVal == null)
                    return 0;
                if (other.Type == IdType.String)
                {
                    return StringVal?.CompareTo(other.StringVal) ?? 1;
                }
                else
                {
                    return StringVal?.CompareTo(other.IntVal.ToString()) ?? 1;
                }
            }
            else
            {
                if (other.Type == IdType.Int)
                {
                    return IntVal.CompareTo(other.IntVal);
                }
                else
                {
                    return this.IntVal.ToString().CompareTo(other.StringVal);
                }
            }
        }

        public bool Equals(StringOrIntId other)
        {
            return this.CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is StringOrIntId id)
            {
                return this.Equals(id);
            }
            return false;
        }

        public override int GetHashCode()
        {
            HashCode hc = new HashCode();
            hc.Add(this.Type);
            if (this.Type == IdType.String)
            {
                hc.Add(this.StringVal);
            }
            else
            {
                hc.Add(IntVal);
            }
            return hc.ToHashCode();
        }

        public override string ToString()
        {
            if (this.Type == IdType.Int)
            {
                return this.IntVal.ToString();
            }
            else
            {
                return this.StringVal;
            }
        }

        public static bool operator ==(StringOrIntId left, StringOrIntId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StringOrIntId left, StringOrIntId right)
        {
            return !(left == right);
        }

        public static bool operator <(StringOrIntId left, StringOrIntId right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(StringOrIntId left, StringOrIntId right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(StringOrIntId left, StringOrIntId right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(StringOrIntId left, StringOrIntId right)
        {
            return left.CompareTo(right) >= 0;
        }
    }


    public class StringOrIntIdConverter : JsonConverter<StringOrIntId>
    {
        public override StringOrIntId ReadJson(JsonReader reader, Type objectType, StringOrIntId existingValue, bool hasExistingValue, JsonSerializer serializer)
        {

            if (reader.TokenType == JsonToken.String)
            {
                if (reader.ValueType == typeof(string))
                {
                    string strVal = (string)reader.Value;
                    if (long.TryParse(strVal, out long ival))
                    {
                        return new StringOrIntId(ival);
                    }
                    else
                    {
                        return new StringOrIntId(strVal);
                    }
                }
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                if (reader.ValueType.IsAssignableTo(typeof(long)))
                {
                    return new StringOrIntId((long)reader.Value);
                }
            }
            return new(); // return null id if unable to convert correctly
        }


        public override void WriteJson(JsonWriter writer, StringOrIntId value, JsonSerializer serializer)
        {
            if (value.Type == StringOrIntId.IdType.String)
            {
                writer.WriteValue(value.StringVal);
            }
            else if (value.Type == StringOrIntId.IdType.Int)
            {
                writer.WriteValue(value.IntVal);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
