using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;

namespace ScorebiniTwitchApi.Models
{
    public class DateTimeToIs08061Converter : ValueConverter<DateTime, string>
    {
        public DateTimeToIs08061Converter() : base(Serialize, Deserialize, null)
        {
        }

        static Expression<Func<string, DateTime>> Deserialize = x => DateTime.Parse(x).ToUniversalTime();
        static Expression<Func<DateTime, string>> Serialize = x => x.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
    }
}
