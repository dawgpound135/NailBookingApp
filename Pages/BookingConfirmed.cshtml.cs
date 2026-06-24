using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace NailBookingApp.Pages;

public class BookingConfirmedModel : PageModel
{
    public string? Name { get; private set; }
    public string? Service { get; private set; }
    public string? AppointmentTime { get; private set; }
    public string? AppointmentDate { get; private set; }

    public void OnGet(string? name, string? service, string? date, string? time)
    {
        Name = name;
        Service = service;
        AppointmentTime = time;

        if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            AppointmentDate = parsedDate.ToString("dddd, dd MMMM yyyy");
        }
    }
}
