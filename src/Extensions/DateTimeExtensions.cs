using Adliance.Kimai.KimaiClient.Models;

namespace Adliance.Kimai.Extensions;

public static class DateTimeExtensions
{
    extension(DateOnly day)
    {
        private DateTime ToDateTime()
        {
            return day.ToDateTime(new TimeOnly(0, 0, 0));
        }

        public bool IsPublicHoliday(Data data)
        {
            return data.PublicHolidays.Any(x => DateOnly.FromDateTime(x.Date) == day);
        }

        public double MinutesToDays(double minutes, Configuration.User user)
        {
            var employment = user.Employments.SingleOrDefault(x => day >= x.Begin && day <= x.End);
            if (employment == null) throw new Exception($"No employment found for {user.Username} on {day}.");
            var expectedMinutes = employment.GetExpectedMinutes(day);
            if (expectedMinutes > 0) return minutes / expectedMinutes;
            return 0;
        }

        public bool IsVacationDay(Configuration.User user, Data data)
        {
            return day.GetAbsences(user, data).Any(x => x.IsVacation);
        }

        public bool IsOtherAbsence(Configuration.User user, Data data)
        {
            return day.GetAbsences(user, data).Count > 0;
        }

        public bool IsHomeOffice(Configuration.User user, Data data)
        {
            return day.GetTimesheets(user, data).Any(x => x.IsHomeOffice);
        }

        public double GetWorkedMinutes(Configuration.User user, Data data)
        {
            var result = 0.0;
            var timesheets = day.GetTimesheets(user, data);

            foreach (var t in timesheets)
            {
                var begin = t.Begin;
                var end = t.End;
                if (begin < day.ToDateTime()) begin = day.ToDateTime();
                if (end > day.ToDateTime().AddDays(1)) end = day.ToDateTime().AddDays(1);

                var minutes = end - begin;
                result += minutes.TotalMinutes;
            }

            return result;
        }

        private List<Timesheet> GetTimesheets(Configuration.User user, Data data)
        {
            var kimaiUser = data.Users.Single(x => x.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
            return data.Timesheets
                .Where(x => x.User == kimaiUser.Id)
                .Where(x => day == DateOnly.FromDateTime(x.Begin) || day == DateOnly.FromDateTime(x.End))
                .ToList();
        }

        private List<Absence> GetAbsences(Configuration.User user, Data data)
        {
            var kimaiUser = data.Users.Single(x => x.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
            var absences = data.Absences
                .Where(x => x.User?.Id == kimaiUser.Id)
                .Where(x => day == x.DateOnly)
                .ToList();
            if (absences.Count > 1) throw new Exception($"More than one absence found for {user.Username} on {day}.");
            return absences;
        }
    }
}
