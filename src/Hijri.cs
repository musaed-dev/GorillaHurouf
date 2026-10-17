using System;
using System.Globalization;
using System.Text;

namespace GorillaHurouf
{
    // The Umm al-Qura calendar (the Saudi one) from the runtime, written in Arabic. No Unity in this file, so it is tested outside the game.
    internal static class HijriDate
    {
        static readonly string[] Months =
        {
            "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
            "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
        };

        // "25 جمادى الأولى 1447 هـ" in logical order (fix it for display with SetArabic or Fix); null outside the calendar's range (1900 to 2077).
        public static string Format(DateTime date, bool arabicDigits)
        {
            var cal = new UmAlQuraCalendar();
            if (date < cal.MinSupportedDateTime || date > cal.MaxSupportedDateTime) return null;
            string text = cal.GetDayOfMonth(date) + " " + Months[cal.GetMonth(date) - 1] + " " + cal.GetYear(date) + " هـ";
            if (!arabicDigits) return text;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text) sb.Append(c >= '0' && c <= '9' ? (char)(0x0660 + (c - '0')) : c);
            return sb.ToString();
        }
    }
}
