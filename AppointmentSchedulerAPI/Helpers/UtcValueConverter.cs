using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class UtcValueConverter : ValueConverter<DateTime, DateTime>
{
    // הגדרת המרה לשליפת נתונים מה-DB
    public UtcValueConverter()
        : base(
            v => v, // איך לשמור ב-DB: כפי שהוא
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) // איך לשלוף: תמיד כ-UTC
    {
    }
}