using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using NailBookingApp.Models;

namespace NailBookingApp.Services;

public class GoogleCalendarService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private static readonly TimeSpan SouthAfricaOffset = TimeSpan.FromHours(2);

    public GoogleCalendarService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    private CalendarService CreateCalendarService()
    {
        var serviceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_JSON");

        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            var localServiceAccountPath = Path.Combine(_environment.ContentRootPath, "hottiebox-nail-appointments-c8657048ac9e.json");

            if (File.Exists(localServiceAccountPath))
            {
                serviceAccountJson = File.ReadAllText(localServiceAccountPath);
            }
        }

        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            throw new Exception("Google service account JSON is missing. Set GOOGLE_SERVICE_ACCOUNT_JSON on the host, or keep the local service-account JSON file in the project folder for development.");
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
        var calendarId = GetCalendarId();

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
        // comment 
    }

    public async Task<bool> IsSlotAvailableAsync(BookingRequest booking)
    {
        var calendarId = GetCalendarId();

        var service = CreateCalendarService();

        var duration = GetServiceDuration(booking.Service);

        var timeParts = booking.AppointmentTime.Split(':');
        var hour = int.Parse(timeParts[0]);
        var minute = int.Parse(timeParts[1]);

        var startDateTime = booking.AppointmentDate.Date.AddHours(hour).AddMinutes(minute);
        var endDateTime = startDateTime.AddMinutes(duration);

        var request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = new DateTimeOffset(startDateTime, SouthAfricaOffset);
        request.TimeMaxDateTimeOffset = new DateTimeOffset(endDateTime, SouthAfricaOffset);
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

        var calendarId = GetCalendarId();
        var calendarService = CreateCalendarService();
        var serviceDuration = GetServiceDuration(service);

        var dayStart = new DateTimeOffset(date.Date, SouthAfricaOffset);
        var dayEnd = new DateTimeOffset(date.Date.AddDays(1), SouthAfricaOffset);

        var request = calendarService.Events.List(calendarId);
        request.TimeMinDateTimeOffset = dayStart;
        request.TimeMaxDateTimeOffset = dayEnd;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();
        var bookedEvents = events.Items ?? new List<Event>();

        return allSlots
            .Where(slot => IsSlotAvailable(slot, date, serviceDuration, bookedEvents))
            .ToList();
    }

    private static bool IsSlotAvailable(string slot, DateTime date, int durationInMinutes, IList<Event> bookedEvents)
    {
        var timeParts = slot.Split(':');
        var hour = int.Parse(timeParts[0]);
        var minute = int.Parse(timeParts[1]);

        var slotStart = new DateTimeOffset(date.Date.AddHours(hour).AddMinutes(minute), SouthAfricaOffset);
        var slotEnd = slotStart.AddMinutes(durationInMinutes);

        return !bookedEvents.Any(calendarEvent => EventOverlaps(calendarEvent, slotStart, slotEnd));
    }

    private static bool EventOverlaps(Event calendarEvent, DateTimeOffset slotStart, DateTimeOffset slotEnd)
    {
        var eventStart = calendarEvent.Start?.DateTimeDateTimeOffset;
        var eventEnd = calendarEvent.End?.DateTimeDateTimeOffset;

        if (!eventStart.HasValue && DateTime.TryParse(calendarEvent.Start?.Date, out var startDate))
        {
            eventStart = new DateTimeOffset(startDate, SouthAfricaOffset);
        }

        if (!eventEnd.HasValue && DateTime.TryParse(calendarEvent.End?.Date, out var endDate))
        {
            eventEnd = new DateTimeOffset(endDate, SouthAfricaOffset);
        }

        if (!eventStart.HasValue || !eventEnd.HasValue)
        {
            return false;
        }

        return eventStart.Value < slotEnd && eventEnd.Value > slotStart;
    }

    private string GetCalendarId()
    {
        var calendarId = _configuration["GoogleCalendar:CalendarId"];

        if (string.IsNullOrWhiteSpace(calendarId))
        {
            throw new Exception("Google Calendar ID is missing. Set GoogleCalendar:CalendarId locally or GoogleCalendar__CalendarId on the host.");
        }

        return calendarId;
    }
}
