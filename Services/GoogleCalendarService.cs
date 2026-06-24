using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using NailBookingApp.Models;

namespace NailBookingApp.Services;

public class GoogleCalendarService
{
    private readonly IConfiguration _configuration;

    public GoogleCalendarService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private CalendarService CreateCalendarService()
    {
        var serviceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_JSON");

        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            throw new Exception("Google service account JSON is missing from environment variables.");
        }

        var credential = GoogleCredential
            .FromJson(serviceAccountJson)
            .CreateScoped(CalendarService.Scope.Calendar);

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Nail Booking App"
        });
    }

    public async Task<string> CreateBookingEventAsync(BookingRequest booking)
    {
        var calendarId = _configuration["GoogleCalendar:CalendarId"];

        var service = CreateCalendarService();

        var duration = GetServiceDuration(booking.Service);

        var timeParts = booking.AppointmentTime.Split(':');
        var hour = int.Parse(timeParts[0]);
        var minute = int.Parse(timeParts[1]);

        var startDateTime = booking.AppointmentDate.Date.AddHours(hour).AddMinutes(minute);
        var endDateTime = startDateTime.AddMinutes(duration);

        var calendarEvent = new Event
        {
            Summary = $"{booking.Name} - {booking.Service}",
            Description =
                $"Client: {booking.Name}\n" +
                $"Phone: {booking.PhoneNumber}\n" +
                $"Service: {booking.Service}\n" +
                $"Notes: {booking.Notes}\n" +
                $"Status: Awaiting WhatsApp confirmation",
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(startDateTime, TimeSpan.FromHours(2)),
                TimeZone = "Africa/Johannesburg"
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(endDateTime, TimeSpan.FromHours(2)),
                TimeZone = "Africa/Johannesburg"
            }
        };

        var request = service.Events.Insert(calendarEvent, calendarId);
        var createdEvent = await request.ExecuteAsync();

        return createdEvent.HtmlLink;
    }

    private int GetServiceDuration(string service)
    {
        return 150;
    }

    public async Task<bool> IsSlotAvailableAsync(BookingRequest booking)
    {
    var calendarId = _configuration["GoogleCalendar:CalendarId"];

    var service = CreateCalendarService();

    var duration = GetServiceDuration(booking.Service);

    var timeParts = booking.AppointmentTime.Split(':');
    var hour = int.Parse(timeParts[0]);
    var minute = int.Parse(timeParts[1]);

    var startDateTime = booking.AppointmentDate.Date.AddHours(hour).AddMinutes(minute);
    var endDateTime = startDateTime.AddMinutes(duration);

    var request = service.Events.List(calendarId);
    request.TimeMinDateTimeOffset = new DateTimeOffset(startDateTime, TimeSpan.FromHours(2));
    request.TimeMaxDateTimeOffset = new DateTimeOffset(endDateTime, TimeSpan.FromHours(2));
    request.SingleEvents = true;
    request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

    var events = await request.ExecuteAsync();

    return events.Items == null || events.Items.Count == 0;
    }

    public async Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date, string service)
    {
    var allSlots = new List<string>
    {
        "09:00", "10:00", "11:00", "12:00",
        "13:00", "14:00", "15:00", "16:00", "17:00"
    };

    var availableSlots = new List<string>();

    foreach (var slot in allSlots)
    {
        var testBooking = new BookingRequest
        {
            AppointmentDate = date,
            AppointmentTime = slot,
            Service = service
        };

        var isAvailable = await IsSlotAvailableAsync(testBooking);

        if (isAvailable)
        {
            availableSlots.Add(slot);
        }
    }

    return availableSlots;
    }
}