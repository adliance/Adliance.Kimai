using System.Net;
using Adliance.Kimai.Client.Exceptions;
using Adliance.Kimai.Client.Models;

namespace Adliance.Kimai.Client.Extensions;

public static class KimaiClientExtensions
{
    public static async Task<List<User>> GetUsersAsync(this KimaiClient client)
    {
        try
        {
            return await client.GetPaginated<User>("/api/users?visible=3");
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return [await client.GetRecord<User>("/api/users/me")];
        }
    }

    public static async Task<List<Timesheet>> GetPaginatedTimesheetsAsync(this KimaiClient client, int[] userIds)
    {
        var query = userIds.Aggregate("", (current, userId) => current + $"users[]={userId}&");
        return await client.GetPaginated<Timesheet>($"api/timesheets?{query.TrimEnd('&')}");
    }

    public static async Task<List<Absence>> GetAbsencesAsync(this KimaiClient client, IEnumerable<int> userIds)
    {
        var result = new List<Absence>();
        foreach (var userId in userIds)
        {
            result.AddRange(await client.GetPaginated<Absence>($"/api/absences?user={userId}"));
        }

        return result;
    }
}
