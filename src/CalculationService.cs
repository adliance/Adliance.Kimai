using System.Globalization;
using Adliance.Kimai.Extensions;

namespace Adliance.Kimai;

public class CalculationService(Configuration config, Data data)
{
    public void Calculate()
    {
        foreach (var u in config.Users) CalculateUser(u);
    }

    private void CalculateUser(Configuration.User user)
    {
        var kimaiUser = data.Users.SingleOrDefault(x => x.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));
        if (kimaiUser == null) throw new Exception($"User {user.Username} not found in Kimai.");
        user.Name = kimaiUser.Title;

        foreach (var e in user.Employments)
        {
            if (e.End > DateOnly.FromDateTime(DateTime.Today)) e.End = DateOnly.FromDateTime(DateTime.Today); // only calculate up to today, default value is DateOnly.Max
            if (e.End < e.Begin) throw new Exception($"End date {e.End} is before start date {e.Begin}.");

            var currentDay = e.Begin;
            while (currentDay <= e.End)
            {
                var expectedMinutes = e.GetExpectedMinutes(currentDay);
                var earnedVacationMinutes = expectedMinutes / (e.Weekdays.Length * 5d);

                if (currentDay.IsPublicHoliday(data))
                {
                    user.PublicHolidayDays++;
                }
                else if (currentDay.IsVacationDay(user, data))
                {
                    if (expectedMinutes > 0)
                    {
                        user.RemainingVacationMinutes -= expectedMinutes;
                        user.VacationDays++;
                    }
                }
                else if (currentDay.IsOtherAbsence(user, data))
                {
                    user.OtherAbsenceDays++;
                }
                else
                {
                    if (expectedMinutes > 0)
                    {
                        if (currentDay.IsHomeOffice(user, data)) user.HomeOfficeDays++;
                        user.ExpectedMinutes += expectedMinutes;
                        user.RemainingVacationMinutes += earnedVacationMinutes;
                    }

                    user.WorkedMinutes += currentDay.GetWorkedMinutes(user, data);
                }

                currentDay = currentDay.AddDays(1);
            }
        }
    }
}
